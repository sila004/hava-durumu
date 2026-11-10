using AutoMapper;
using Serilog.Extensions.Logging;

namespace Intern.Service.AutoMapper
{
    public static class ObjectMapper
    {
        private static readonly Lazy<IMapper> lazy = new Lazy<IMapper>(() =>
        {
            // Lisans anahtarı kodda yok: AutoMapper AUTOMAPPER_LICENSE_KEY ortam değişkenini kendisi okur.
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<DtoMapper>();
            }, new SerilogLoggerFactory());

            return config.CreateMapper();
        });

        public static IMapper Mapper => lazy.Value;
    }
}
