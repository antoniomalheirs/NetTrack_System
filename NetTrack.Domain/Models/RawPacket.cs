using System;

namespace NetTrack.Domain.Models
{
    public class RawPacket
    {
        public DateTime Timeval { get; set; }
        public byte[] Data { get; set; } 
        public int DataLength { get; set; }
        
        // Reference to the pool buffer if rented
        public byte[]? RentedBuffer { get; set; }

        public RawPacket(DateTime timeval, byte[] data, int linkLayerType)
        {
            Timeval = timeval;
            Data = data;
            DataLength = data.Length;
            LinkLayerType = linkLayerType;
        }

        public RawPacket(DateTime timeval, byte[] rentedBuffer, int actualLength, int linkLayerType)
        {
            Timeval = timeval;
            RentedBuffer = rentedBuffer;
            Data = rentedBuffer; // Point Data to the rented buffer for backward compat (careful with Length!)
            DataLength = actualLength;
            LinkLayerType = linkLayerType;
        }

        public int LinkLayerType { get; set; }
    }
}
