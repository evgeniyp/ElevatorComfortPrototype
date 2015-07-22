using System;

namespace Parsers
{
    public interface IByteArrDataParser
    {
        void HandleData(byte[] buffer, int bytesToRead);
        event Action<string, object> DataParsed;
    }
}