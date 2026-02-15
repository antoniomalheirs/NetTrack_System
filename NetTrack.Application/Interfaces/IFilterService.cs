using NetTrack.Domain.Models;
using System;
using System.Linq.Expressions;

namespace NetTrack.Application.Interfaces
{
    public interface IFilterService
    {
        Predicate<PacketModel> CompileFilter(string filterString);
        System.Collections.Generic.IEnumerable<string> GetSuggestions(string input);
    }
}
