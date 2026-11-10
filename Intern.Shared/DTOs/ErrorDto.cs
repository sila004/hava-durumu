namespace Intern.Shared.DTOs
{
    public class ErrorDto
    {
        public List<string> Errors { get; private set; }
        public bool IsShow { get; private set; }

        /// <summary>
        /// Kararlı hata kodu — frontend i18n mapping için kullanılır.
        /// Null ise frontend Errors[0]'ı göstermeye düşer.
        /// </summary>
        public string? Code { get; private set; }

        /// <summary>
        /// i18n mesajındaki yer tutucular için değerler (ör. {{chain}}). Frontend bunları
        /// `errors.codes.{Code}` çevirisine interpolasyon parametresi olarak geçer.
        /// Null ise çeviri parametresiz kullanılır.
        /// </summary>
        public Dictionary<string, string>? Params { get; private set; }

        public ErrorDto()
        {
            Errors = new List<string>();
        }

        public ErrorDto(string error, bool isShow): this()
        {
            Errors.Add(error);
            IsShow = isShow;
        }

        public ErrorDto(List<string> errors, bool isShow):this()
        {
            Errors.AddRange(errors);
            IsShow = isShow;
        }

        public ErrorDto(string code, string error, bool isShow) : this()
        {
            Code = code;
            Errors.Add(error);
            IsShow = isShow;
        }

        public ErrorDto(string code, string error, Dictionary<string, string> parameters, bool isShow)
            : this(code, error, isShow)
        {
            Params = parameters;
        }

        public override string ToString()
        {
            return String.Join("#", Errors);
        }
    }
}
