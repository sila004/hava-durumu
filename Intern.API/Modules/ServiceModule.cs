using Autofac;
using Intern.Data;
using Intern.Core.UnitOfWork;
using Module = Autofac.Module;
using System.Reflection;
using Intern.Data.Repositories;
using Intern.Service.Services;
using Intern.Core.Services;
using Intern.Core.Repositories;
using Intern.Service.AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace Intern.API.Modules
{
    public class ServiceModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // DbContext, AddDbContext'in kaydettiği AppDbContext'ten çözülür; aynı istekte tek bağlam olur.
            // (RegisterType<AppDbContext>().As<DbContext>() ikinci bir bağlam üretir: repository bir bağlama ekler,
            // UnitOfWork öbürünü kaydeder ve veri sessizce yazılmaz.) Yaşam döngüsünü DI yönetir.
            builder.Register(c => c.Resolve<AppDbContext>()).As<DbContext>().ExternallyOwned();
            builder.RegisterGeneric(typeof(GenericRepository<>)).As(typeof(IGenericRepository<>)).InstancePerLifetimeScope();
            builder.RegisterGeneric(typeof(GenericService<,,>)).As(typeof(IGenericService<,,>)).InstancePerLifetimeScope();

            builder.RegisterType<UnitOfWork>().As<IUnitOfWork>().InstancePerLifetimeScope();

            var apiAssembly = Assembly.GetExecutingAssembly();
            var repoAssembly = Assembly.GetAssembly(typeof(AppDbContext));
            var servicesAssembly = Assembly.GetAssembly(typeof(DtoMapper));

            // Sonu Service/Repository ile biten sınıf kendiliğinden kaydolur; ayrıca kayıt gerekmez.
            builder
                .RegisterAssemblyTypes(apiAssembly, repoAssembly, servicesAssembly)
                .Where(x => x.Name.EndsWith("Repository"))
                .AsImplementedInterfaces().InstancePerLifetimeScope();

            builder
                .RegisterAssemblyTypes(apiAssembly, repoAssembly, servicesAssembly)
                .Where(x => x.Name.EndsWith("Service") && !x.Name.EndsWith("BackgroundService"))
                .AsImplementedInterfaces()
                .AsSelf()
                .InstancePerLifetimeScope();
        }
    }
}
