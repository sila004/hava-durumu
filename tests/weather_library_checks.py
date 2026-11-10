"""Hesaba bağlı kayıtlar, kalıcılık ve veri yalıtımı için HTTP kontrolleri."""
from datetime import datetime, timedelta


def check_library(request):
    library, _ = request("/library")
    assert library["favorites"] == [] and library["preferences"] == {}
    request("/favorites", "PUT", {"city": "Ankara"}, expected=403, custom_header=False)
    request("/favorites", "PUT", {"city": "Atlantis"}, expected=400)
    request("/favorites", "PUT", {"city": "Ankara"})
    request("/favorites", "PUT", {"city": "Ankara"})
    request("/favorites", "PUT", {"city": "Bursa"})
    assert request("/library")[0]["favorites"] == ["Ankara", "Bursa"]
    request("/favorites/Ankara", "DELETE")
    assert request("/library")[0]["favorites"] == ["Bursa"]
    request("/preferences/home", "PUT", {"city": "Ankara"})
    request("/preferences/home", "PUT", {"city": "Bursa"})
    request("/preferences/map", "PUT", {"city": "Ankara"})
    request("/preferences/air-quality", "PUT", {"city": "Bursa"})
    request("/preferences/other", "PUT", {"city": "Bursa"}, expected=400)
    request("/preferences/map", "PUT", {"city": "Atlantis"}, expected=400)
    request("/preferences/map", "PUT", {"city": "Bursa"}, expected=403, custom_header=False)
    assert request("/library")[0]["preferences"] == {"home": "Bursa", "map": "Ankara", "air-quality": "Bursa"}
    assert request("/me")[0]["city"] == "İstanbul"  # Sayfa tercihi posta şehrini değiştirmez.
    assert request("/comparisons")[0] == []
    pair = {"firstCity": "Ankara", "secondCity": "Bursa"}
    request("/comparisons", "POST", pair, expected=403, custom_header=False)
    request("/comparisons", "POST", pair | {"secondCity": "Ankara"}, expected=400)
    request("/comparisons", "POST", pair | {"secondCity": "Atlantis"}, expected=400)
    first, headers = request("/comparisons", "POST", pair | {"firstWeather": {"temperature": 999}})
    assert first["firstWeather"]["temperature"] == 15 and first["secondWeather"]["humidity"] == 65
    assert first["comparedUtc"] and "no-store" in headers.get("Cache-Control", "")
    restored = request("/comparisons")[0][0]
    assert {k: v for k, v in restored.items() if k != "comparedUtc"} == {k: v for k, v in first.items() if k != "comparedUtc"}
    assert abs(datetime.fromisoformat(restored["comparedUtc"]) - datetime.fromisoformat(first["comparedUtc"])) <= timedelta(microseconds=1)
    for _ in range(50):
        request("/comparisons", "POST", pair)
    history = request("/comparisons")[0]
    assert len(history) == 50 and first["id"] not in {item["id"] for item in history}
    request("/comparisons", "DELETE", expected=403, custom_header=False)
    print("PASS kişisel kayıtlar: favori ekleme/silme/tekillik, sayfa tercihleri ve posta şehrinin bağımsızlığı")
    print("PASS karşılaştırma: sunucu değerleri, doğrulama, kayıt zamanı, no-store, son 50 kayıt sınırı")


def check_library_isolation(request):
    library = request("/library")[0]
    assert library["favorites"] == [] and library["preferences"] == {}
    assert request("/comparisons")[0] == []
    request("/favorites", "PUT", {"city": "Ankara"})
    request("/preferences/home", "PUT", {"city": "Ankara"})
    request("/comparisons", "POST", {"firstCity": "Ankara", "secondCity": "Bursa"})
    request("/comparisons", "DELETE")
    assert request("/comparisons")[0] == []
    print("PASS hesap yalıtımı: başka hesabın favorileri, tercihleri ve geçmişi görünmez; temizleme yalnız kendi kayıtlarını siler")
