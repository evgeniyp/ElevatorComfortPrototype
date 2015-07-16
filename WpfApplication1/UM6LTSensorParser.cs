using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class PtChar
{
    private readonly byte _value;
    public PtChar(byte value) { _value = Convert.ToByte(value); }
    public bool HasData()
    {
        return (_value & (byte)128) != 0;
    }
    public bool IsBatchOperation()
    {
        return (_value & (byte)64) != 0;
    }
    public int GetDataLength()
    {
        return IsBatchOperation() ? (byte)(_value >> 2) & (byte)15 : 1;
    }
}

public class UM6LTSensorParser
{
    private const float ACCEL_MULTIPLY_FACTOR = 0.000183105f;
    private const byte UM6_ACCEL_PROC_XY = 0x5E;
    private const byte UM6_ACCEL_PROC_Z = 0x5F;
    private readonly byte[] NEW_PACKET_SIGNATURE = { 0x73, 0x6E, 0x70 };
    private bool _gotFirstSignature = false;
    private byte[] _buffer = new byte[1024];
    private int _bufferPosition = 0;

    public Action<float> OnX, OnY, OnZ;

    public void HandleData(byte[] buffer, int bytesToRead)
    {
        Array.Copy(buffer, 0, _buffer, _bufferPosition, bytesToRead);
        _bufferPosition += bytesToRead;

        int snpIndex;
        do
        {
            snpIndex = -1;
            for (int i = 0; i <= _bufferPosition - NEW_PACKET_SIGNATURE.Length; i++) // looking for new packet signature index
            {
                if (_buffer[i + 0] == NEW_PACKET_SIGNATURE[0] &&
                    _buffer[i + 1] == NEW_PACKET_SIGNATURE[1] &&
                    _buffer[i + 2] == NEW_PACKET_SIGNATURE[2])
                {
                    snpIndex = i;
                    break;
                }
            }

            if (snpIndex > -1)
            {
                if (_gotFirstSignature)
                {
                    var packet = new byte[snpIndex];
                    Array.Copy(_buffer, 0, packet, 0, snpIndex); // locating
                    PacketReceived(packet);
                }
                else
                {
                    _gotFirstSignature = true; // omitting first incomplete packet
                }

                // moving buffer to start
                for (int i = 0; i < _bufferPosition - snpIndex - NEW_PACKET_SIGNATURE.Length; i++)
                {
                    _buffer[i] = _buffer[i + snpIndex + NEW_PACKET_SIGNATURE.Length];
                }
                // clearing old bytes
                for (int i = _bufferPosition - snpIndex - NEW_PACKET_SIGNATURE.Length; i < _bufferPosition; i++)
                {
                    _buffer[i] = 0x00;
                }
                _bufferPosition -= snpIndex + NEW_PACKET_SIGNATURE.Length;
            }
        } while (snpIndex > -1);
    }

    private void PacketReceived(byte[] packet)
    {
        //Console.WriteLine("PT: " + Convert.ToString(packet[0], 2).PadLeft(8, '0') + " OTHER: " + BitConverter.ToString(packet, 1, packet.Length - 1));

        PtChar ptChar = new PtChar(packet[0]);
        if (ptChar.HasData())
        {
            byte address = packet[1];
            if (ptChar.IsBatchOperation())
            {
                var dataLength = ptChar.GetDataLength();
                for (int i = 0; i < dataLength; i++)
                {
                    byte[] data = new byte[4];
                    Array.Copy(packet, 2 + 4 * i, data, 0, 4);
                    ParseData(address, data);
                    address++;
                }
            }
            else
            {
                byte[] data = new byte[4];
                Array.Copy(packet, 2, data, 0, 4);
                ParseData(address, data);
            }
        }
    }

    private void ParseData(byte address, byte[] data)
    {
        switch (address)
        {
            case UM6_ACCEL_PROC_XY:
                Int16 integerValueX = (Int16)(((Int16)data[0]) << 8 | data[1]);
                float floatValueX = (float)integerValueX * ACCEL_MULTIPLY_FACTOR;
                if (OnX != null) { OnX(floatValueX); }

                Int16 integerValueY = (Int16)(((Int16)data[2]) << 8 | data[3]);
                float floatValueY = (float)integerValueY * ACCEL_MULTIPLY_FACTOR;
                if (OnY != null) { OnY(floatValueY); }
                break;
            case UM6_ACCEL_PROC_Z:
                Int16 integerValueZ = (Int16)(((Int16)data[0]) << 8 | data[1]);
                float floatValueZ = (float)integerValueZ * ACCEL_MULTIPLY_FACTOR;
                if (OnZ != null) { OnZ(floatValueZ); }
                break;
            default:
                break;
        }
    }

    public byte[] GetAccelRequestPacket()
    {
        byte[] packet = { NEW_PACKET_SIGNATURE[0], NEW_PACKET_SIGNATURE[1], NEW_PACKET_SIGNATURE[2],
                          0x00, // packet type
                          UM6_ACCEL_PROC_Z,
                          0x00, 0x00 // reserved for checksum
                        };
        var checksumBytes = BitConverter.GetBytes(Checksum(packet));
        packet[packet.Length - 2] = checksumBytes[1];
        packet[packet.Length - 1] = checksumBytes[0];

        return packet;
    }

    private ushort Checksum(byte[] bytes)
    {
        ushort sum = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            sum += bytes[i];
        }
        return sum;
    }
}
