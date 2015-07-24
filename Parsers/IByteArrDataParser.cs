using System;

namespace Parsers
{
    public interface IByteArrDataParser
    {
        void Reset();
        void HandleData(byte[] buffer, int bytesToRead);
        event Action<string, object> DataParsed;
    }
}