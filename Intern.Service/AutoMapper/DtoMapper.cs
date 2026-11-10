using AutoMapper;
using Intern.Entities.Concretes;
using Intern.Entities.DTOs.Ability;
using Intern.Entities.DTOs.Employee;

namespace Intern.Service.AutoMapper
{
    public class DtoMapper : Profile
    {
        public DtoMapper()
        {
            CreateMap<Ability, AbilityDto>().ReverseMap();
            CreateMap<Ability, CreateAbilityDto>().ReverseMap();

            CreateMap<Employee, EmployeeDto>().ReverseMap();
            CreateMap<Employee, CreateEmployeeDto>().ReverseMap();
        }
    }
}
