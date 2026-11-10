"""İzole PostgreSQL + yerel mail klasörüyle uçtan uca test. Gerçek mail göndermez.
Önce: dotnet build tests/WeatherMail.Checks/WeatherMail.Checks.csproj
Sonra: python tests/test_weather_mail.py
"""
import datetime as dt
import email.policy
import email.parser
import http.cookiejar
import http.server
import json
import os
from pathlib import Path
import re
import socket
import subprocess
import sys
import tempfile
import threading
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
PG_BIN = Path(os.environ.get("PG_BIN", "C:/Program Files/PostgreSQL/18/bin"))
WORK = Path(tempfile.mkdtemp(prefix="hava-mail-checks-"))
MAILBOX = WORK / "mail"
FLAGS = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0


def port():
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        return sock.getsockname()[1]


def command(args, **kwargs):
    return subprocess.run([str(arg) for arg in args], check=True, creationflags=FLAGS, **kwargs)


class ForecastHandler(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        day = (dt.datetime.now(dt.timezone.utc) + dt.timedelta(hours=3)).date().isoformat()
        result = {
            "timezone": "Europe/Istanbul",
            "current": {"time": day + "T07:00", "temperature_2m": 15, "apparent_temperature": 14,
                "relative_humidity_2m": 65, "wind_speed_10m": 8, "wind_direction_10m": 40,
                "surface_pressure": 1013, "precipitation": 0, "cloud_cover": 20, "weather_code": 2, "is_day": 1},
            "daily": {"time": [day], "weather_code": [2], "temperature_2m_max": [18], "temperature_2m_min": [11],
                "precipitation_probability_max": [35], "sunrise": [day + "T07:30"], "sunset": [day + "T17:30"]},
            "hourly": {"time": [day + "T09:00"], "temperature_2m": [16], "apparent_temperature": [15],
                "weather_code": [2], "precipitation_probability": [10], "precipitation": [0],
                "wind_speed_10m": [8], "relative_humidity_2m": [60], "is_day": [1], "uv_index": [2]},
        }
        body = json.dumps(result).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, *_):
        pass


db_port, api_port, forecast_port = port(), port(), port()
connection = f"Host=127.0.0.1;Port={db_port};Database=postgres;Username=postgres"
url = f"http://127.0.0.1:{api_port}"
mock = http.server.ThreadingHTTPServer(("127.0.0.1", forecast_port), ForecastHandler)
threading.Thread(target=mock.serve_forever, daemon=True).start()
api = None
pg_started = False
try:
    with (WORK / "init.log").open("w") as output:
        command([PG_BIN / "initdb.exe", "-D", WORK / "pg", "-A", "trust", "-U", "postgres", "--no-locale", "-E", "UTF8"], stdout=output, stderr=subprocess.STDOUT)
    command([PG_BIN / "pg_ctl.exe", "-D", WORK / "pg", "-l", WORK / "postgres.log", "-o", f"-h 127.0.0.1 -p {db_port}", "-w", "start"], stdout=subprocess.DEVNULL)
    pg_started = True
    env = os.environ | {
        "ASPNETCORE_ENVIRONMENT": "Development", "ASPNETCORE_URLS": url,
        "CONNECTION_STRING": connection, "Database__MigrateOnStartup": "true",
        "Mail__Enabled": "true", "Mail__DeliveryMode": "Pickup", "Mail__PickupDirectory": str(MAILBOX),
        "Mail__ApiUrl": url, "Weather__ForecastUrl": f"http://127.0.0.1:{forecast_port}/forecast",
        "WeatherAdmin__Emails__0": "admin@example.test",
        "WeatherAdmin__Emails__1": "ui-admin@example.test",
        "Cors__AllowedOrigins__0": "http://localhost:3000",
    }
    with (WORK / "api.log").open("w") as log:
        api = subprocess.Popen([str(ROOT / "Intern.API/bin/Debug/net9.0/Intern.API.exe")],
            cwd=ROOT / "Intern.API", env=env, stdout=log, stderr=subprocess.STDOUT, creationflags=FLAGS)
    for _ in range(100):
        if api.poll() is not None:
            raise RuntimeError("Test API kapandı; log: " + str(WORK / "api.log"))
        try:
            urllib.request.urlopen(url + "/health", timeout=1).close()
            break
        except (OSError, urllib.error.URLError):
            time.sleep(0.2)
    else:
        raise RuntimeError("API açılmadı; log: " + str(WORK / "api.log"))

    cookies = http.cookiejar.CookieJar()
    client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cookies))

    def request(path, method="GET", data=None, expected=200, custom_header=True, prefix="/api/weather-account"):
        headers = {"Content-Type": "application/json"}
        if custom_header:
            headers["X-Weather-Client"] = "web"
        req = urllib.request.Request(url + prefix + path, method=method, headers=headers,
            data=json.dumps(data).encode() if data is not None else None)
        try:
            response = client.open(req, timeout=10)
        except urllib.error.HTTPError as exc:
            response = exc
        body = response.read().decode("utf-8")
        assert response.code == expected, (path, response.code, expected, body[:300])
        try:
            return json.loads(body), response.headers
        except json.JSONDecodeError:
            return body, response.headers

    def admin_request(path, method="GET", data=None, expected=200, custom_header=True):
        return request(path, method, data, expected, custom_header, prefix="/api/weather-admin")

    protected = [("/overview", "GET", None), ("/accounts", "GET", None), ("/deliveries", "GET", None),
        ("/accounts/00000000-0000-0000-0000-000000000001/subscription", "PUT", {"city": "Ankara", "deliveryTime": "07:00", "dailyEnabled": True}),
        ("/accounts/00000000-0000-0000-0000-000000000001/retry", "POST", None)]
    for path, method, data in protected:
        admin_request(path, method, data, expected=401)

    state, _ = request("/status")
    assert state["mailReady"] and state["developmentPickup"]
    request("/me", expected=401)
    request("/request-code", "POST", {"email": "first@example.test"}, expected=403, custom_header=False)
    preview, _ = request("/preview")
    assert "Canlı tahmin değildir" in preview
    (WORK / "mail-preview.html").write_text(preview, encoding="utf-8")
    challenge, _ = request("/request-code", "POST", {"email": "first@example.test"})
    request("/request-code", "POST", {"email": "first@example.test"}, expected=429)

    def read_code():
        path = max(MAILBOX.glob("*.eml"), key=lambda f: f.stat().st_mtime_ns)
        message = email.parser.BytesParser(policy=email.policy.default).parsebytes(path.read_bytes())
        return re.search(r"giriş kodun: ([0-9]{6})", message.get_body(preferencelist=("plain",)).get_content()).group(1)

    code = read_code()
    wrong = code[:-1] + str((int(code[-1]) + 1) % 10)
    request("/verify", "POST", {"challengeId": challenge["challengeId"], "code": wrong}, expected=400)
    profile, headers = request("/verify", "POST", {"challengeId": challenge["challengeId"], "code": code})
    cookie_header = headers.get("Set-Cookie", "").lower()
    assert "httponly" in cookie_header and "samesite=lax" in cookie_header
    assert profile["email"] == "first@example.test" and not profile["dailyEnabled"] and profile["deliveryTime"] == "07:00"
    assert not profile["isAdmin"]
    for path, method, data in protected:
        admin_request(path, method, data, expected=403)
    request("/verify", "POST", {"challengeId": challenge["challengeId"], "code": code}, expected=400)
    request("/me")
    request("/subscription", "PUT", {"city": "Ankara", "deliveryTime": "07:00", "dailyEnabled": True}, expected=403, custom_header=False)
    request("/subscription", "PUT", {"city": "Atlantis", "deliveryTime": "07:00", "dailyEnabled": True}, expected=400)
    request("/subscription", "PUT", {"city": "Ankara", "deliveryTime": "25:00", "dailyEnabled": True}, expected=400)
    profile, _ = request("/subscription", "PUT", {"city": "Ankara", "deliveryTime": "07:00", "dailyEnabled": True})
    assert profile["dailyEnabled"] and profile["city"] == "Ankara" and profile["nextDeliveryUtc"]
    request("/test-mail", "POST")
    daily_path = max(MAILBOX.glob("*.eml"), key=lambda f: f.stat().st_mtime_ns)
    daily = email.parser.BytesParser(policy=email.policy.default).parsebytes(daily_path.read_bytes())
    daily_html = daily.get_body(preferencelist=("html",)).get_content()
    assert "Ankara" in daily_html and "18°" in daily_html and "Canlı tahmin değildir" not in daily_html
    (WORK / "daily-mail.html").write_text(daily_html, encoding="utf-8")
    request("/test-mail", "POST", expected=429)
    token = re.search(r"/unsubscribe/([A-F0-9]{64})", daily_html).group(1)
    request("/unsubscribe/" + token)  # Bağlantıyı açmak tercihi değiştirmez.
    assert request("/me")[0]["dailyEnabled"]
    request("/unsubscribe/" + token, "POST")
    assert not request("/me")[0]["dailyEnabled"]
    request("/logout", "POST")
    request("/me", expected=401)

    locked, _ = request("/request-code", "POST", {"email": "lockout@example.test"})
    lock_code = read_code()
    bad = lock_code[:-1] + str((int(lock_code[-1]) + 1) % 10)
    for _ in range(5):
        request("/verify", "POST", {"challengeId": locked["challengeId"], "code": bad}, expected=400)
    request("/verify", "POST", {"challengeId": locked["challengeId"], "code": lock_code}, expected=400)
    expired, _ = request("/request-code", "POST", {"email": "expired@example.test"})
    expired_code = read_code()
    command([PG_BIN / "psql.exe", "-h", "127.0.0.1", "-p", str(db_port), "-U", "postgres", "-d", "postgres", "-c",
        'UPDATE "WeatherLoginChallenges" SET "ExpiresUtc" = CURRENT_TIMESTAMP - interval \'1 minute\' WHERE "Id" = \'' + expired["challengeId"] + "';"], stdout=subprocess.DEVNULL)
    request("/verify", "POST", {"challengeId": expired["challengeId"], "code": expired_code}, expected=400)
    # Önceki senaryolar IP başına beş giriş isteğine ulaştı. Yeni host ile admin
    # senaryosuna ayrı hız sınırı penceresi aç; veritabanı korunur.
    api.terminate()
    api.wait(timeout=10)
    with (WORK / "api.log").open("a") as log:
        api = subprocess.Popen([str(ROOT / "Intern.API/bin/Debug/net9.0/Intern.API.exe")],
            cwd=ROOT / "Intern.API", env=env, stdout=log, stderr=subprocess.STDOUT, creationflags=FLAGS)
    for _ in range(100):
        if api.poll() is not None:
            raise RuntimeError("Admin test API kapandı; log: " + str(WORK / "api.log"))
        try:
            urllib.request.urlopen(url + "/health", timeout=1).close()
            break
        except (OSError, urllib.error.URLError):
            time.sleep(0.2)
    else:
        raise RuntimeError("Admin test API açılmadı")
    admin_challenge, _ = request("/request-code", "POST", {"email": "ADMIN@example.test"})
    admin_profile, _ = request("/verify", "POST", {"challengeId": admin_challenge["challengeId"], "code": read_code()})
    assert admin_profile["isAdmin"] and admin_profile["email"] == "admin@example.test"
    summary, headers = admin_request("/overview")
    assert summary["totalAccounts"] == 2 and summary["activeSubscriptions"] == 0 and "no-store" in headers.get("Cache-Control", "")
    assert summary["sentToday"] == 0 and summary["failedToday"] == 0
    members, _ = admin_request("/accounts?search=first%40example.test")
    assert members["total"] == 1
    member = members["items"][0]
    assert "unsubscribeToken" not in member and "codeHash" not in member
    member_id = member["id"]
    admin_request("/accounts?page=0", expected=400)
    admin_request("/deliveries?page=-1", expected=400)
    assert admin_request("/accounts?enabled=true")[0]["total"] == 0
    assert admin_request("/accounts?search=missing")[0]["total"] == 0
    history, _ = admin_request("/deliveries")
    assert history["total"] == 1 and history["items"][0]["kind"] == "Test" and history["items"][0]["succeeded"]
    assert admin_request("/deliveries?succeeded=false")[0]["total"] == 0
    subscription_path = f"/accounts/{member_id}/subscription"
    preferences = {"city": "İzmir", "deliveryTime": "00:00", "dailyEnabled": True}
    admin_request(subscription_path, "PUT", preferences, expected=403, custom_header=False)
    admin_request(subscription_path, "PUT", preferences | {"city": "Atlantis"}, expected=400)
    admin_request(subscription_path, "PUT", preferences | {"deliveryTime": "25:00"}, expected=400)
    admin_request(subscription_path, "PUT", preferences)
    changed = admin_request("/accounts?search=first")[0]["items"][0]
    assert changed["city"] == "İzmir" and changed["deliveryTime"] == "00:00" and changed["dailyEnabled"]
    retry_path = f"/accounts/{member_id}/retry"
    admin_request(retry_path, "POST", expected=403, custom_header=False)
    admin_request(retry_path, "POST", expected=409)  # Başarısız günlük kayıt yok.

    def sql(statement):
        command([PG_BIN / "psql.exe", "-h", "127.0.0.1", "-p", str(db_port), "-U", "postgres", "-d", "postgres", "-v", "ON_ERROR_STOP=1", "-c", statement], stdout=subprocess.DEVNULL)

    sql(f'''INSERT INTO "WeatherMailDeliveries" ("Id", "AccountId", "City", "Kind", "AttemptedUtc", "Succeeded", "ErrorCode")
        VALUES ('00000000-0000-0000-0000-000000000123', '{member_id}', 'İzmir', 'Daily', CURRENT_TIMESTAMP, FALSE, 'WeatherUnavailable');''')
    admin_request(retry_path, "POST")
    assert admin_request("/overview")[0]["failedToday"] == 1
    assert admin_request("/deliveries?succeeded=false")[0]["items"][0]["errorCode"] == "WeatherUnavailable"
    sql(f'''UPDATE "WeatherMailAccounts" SET "LeaseUntilUtc" = CURRENT_TIMESTAMP + interval '5 minutes' WHERE "Id" = '{member_id}';''')
    admin_request(retry_path, "POST", expected=409)
    admin_request(subscription_path, "PUT", preferences, expected=409)
    sql(f'''UPDATE "WeatherMailAccounts" SET "LeaseUntilUtc" = NULL, "LastSentDate" = (CURRENT_TIMESTAMP AT TIME ZONE 'Europe/Istanbul')::date WHERE "Id" = '{member_id}';''')
    admin_request(retry_path, "POST", expected=409)
    admin_request(subscription_path, "PUT", preferences | {"dailyEnabled": False})
    admin_request(retry_path, "POST", expected=409)
    admin_request("/accounts/00000000-0000-0000-0000-000000000099/retry", "POST", expected=404)
    print("PASS admin: anonim 401, normal hesap 403, yönetici erişimi, no-store ve gizli alanların dışlanması")
    print("PASS admin: arama, filtre, sayfalama doğrulama, abonelik düzenleme, geçmiş ve yeniden deneme sınırları")
    print("PASS giriş, HttpOnly oturum, yeniden kod, yanlış kod, tekrar kullanım, 5 deneme sınırı")
    print("PASS şehir/saat doğrulama, test maili, iptal onayı, çıkış ve yetkisiz erişim")
    checks = command(["dotnet", "run", "--project", ROOT / "tests/WeatherMail.Checks", "--no-build"],
        env=env | {"MAIL_TEST_CONNECTION": connection, "MAIL_TEST_PICKUP": str(WORK / "schedule-mail")},
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8")
    print(checks.stdout)
    print("Test mail tasarımları ve loglar:", WORK, flush=True)
    if "--preview" in sys.argv:
        print("PREVIEW_API_URL=" + url.replace("127.0.0.1", "localhost"), flush=True)
        print("PREVIEW_STOP_FILE=" + str(WORK / "preview.stop"), flush=True)
        while not (WORK / "preview.stop").exists():
            time.sleep(0.25)
finally:
    if api is not None and api.poll() is None:
        api.terminate()
        api.wait(timeout=10)
    if pg_started:
        command([PG_BIN / "pg_ctl.exe", "-D", WORK / "pg", "-m", "fast", "-w", "stop"], stdout=subprocess.DEVNULL)
    mock.shutdown()
