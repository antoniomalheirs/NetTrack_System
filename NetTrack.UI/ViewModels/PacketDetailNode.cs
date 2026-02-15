using System.Collections.ObjectModel;

namespace NetTrack.Client.ViewModels
{
    public class PacketDetailNode
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public ObservableCollection<PacketDetailNode> Children { get; set; } = new ObservableCollection<PacketDetailNode>();

        public PacketDetailNode(string name, string value = "")
        {
            Name = name;
            Value = value;
        }
    }
}
