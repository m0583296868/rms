using AutoMapper;
using RMS.Models;
using RMS.Models.DTO;

namespace RMS
{
    public class DTOProfile : Profile
    {
        public DTOProfile()
        {
            CreateMap<Requests, RequestDto>()
                .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
                .ForMember(d => d.Priority, o => o.MapFrom(s => s.Priority.ToString()));

            CreateMap<StatusHistories, StatusHistoryDto>()
                .ForMember(d => d.PreviousStatus, o => o.MapFrom(s => s.PreviousStatus.ToString()))
                .ForMember(d => d.NewStatus, o => o.MapFrom(s => s.NewStatus.ToString()));
        }
    }
}
