using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameMaker.GameMaker
{
#if DEBUG_JUNK_ENABLED
    class BinaryReader
    {
            public MemoryStream Buffer { get;  set;  }
            public static bool BigEndian = false;
            public BinaryReader() { this.Buffer = new MemoryStream(); }

            public int method_0(int int_0)
            {
                byte[] buffer = this.Buffer.GetBuffer();
                if (BigEndian)
                {
                    byte[] numArray = new byte[] { buffer[int_0 + 3], buffer[int_0 + 2], buffer[int_0 + 1], buffer[int_0] };
                    buffer = numArray;
                    int_0 = 0;
                }
                return BitConverter.ToInt32(buffer, int_0);
            }

            public long method_1(int int_0)
            {
                byte[] buffer = this.Buffer.GetBuffer();
                if (BigEndian)
                {
                    byte[] numArray = new byte[] { buffer[int_0 + 7], buffer[int_0 + 6], buffer[int_0 + 5], buffer[int_0 + 4], buffer[int_0 + 3], buffer[int_0 + 2], buffer[int_0 + 1], buffer[int_0] };
                    buffer = numArray;
                    int_0 = 0;
                }
                return BitConverter.ToInt64(buffer, int_0);
            }

            public float method_2(int int_0)
            {
                byte[] buffer = this.Buffer.GetBuffer();
                if (BigEndian)
                {
                    byte[] numArray = new byte[] { buffer[int_0 + 3], buffer[int_0 + 2], buffer[int_0 + 1], buffer[int_0] };
                    buffer = numArray;
                    int_0 = 0;
                }
                return BitConverter.ToSingle(buffer, int_0);
            }

            public double method_3(int int_0)
            {
                byte[] buffer = this.Buffer.GetBuffer();
                if (BigEndian)
                {
                    byte[] numArray = new byte[] { buffer[int_0 + 7], buffer[int_0 + 6], buffer[int_0 + 5], buffer[int_0 + 4], buffer[int_0 + 3], buffer[int_0 + 2], buffer[int_0 + 1], buffer[int_0] };
                    buffer = numArray;
                    int_0 = 0;
                }
                return BitConverter.ToDouble(buffer, int_0);
            }

            public void method_4(int int_0, int int_1)
            {
                byte[] buffer = this.Buffer.GetBuffer();
                if (!BigEndian)
                {
                    buffer[int_0] = (byte)(int_1 & 255);
                    buffer[int_0 + 1] = (byte)(int_1 >> 8 & 255);
                    buffer[int_0 + 2] = (byte)(int_1 >> 16 & 255);
                    buffer[int_0 + 3] = (byte)(int_1 >> 24 & 255);
                }
                else
                {
                    buffer[int_0] = (byte)(int_1 >> 24 & 255);
                    buffer[int_0 + 1] = (byte)(int_1 >> 16 & 255);
                    buffer[int_0 + 2] = (byte)(int_1 >> 8 & 255);
                    buffer[int_0 + 3] = (byte)(int_1 & 255);
                }
            }

            public void method_5(params int[] int_0)
            {
                int[] int0 = int_0;
                for (int i = 0; i < (int)int0.Length; i++)
                {
                    int num = int0[i];
                    this.Buffer.smethod_13(num);
                }
            }

            public static int smethod_0(int int_0, int int_1)
            {
                return int_0 | int_1 << 4;
            }

            public static int smethod_1(int int_0, int int_1)
            {
                return int_0 << 24 | int_1 << 16;
            }

            public static int smethod_2(int int_0, int int_1, int int_2)
            {
                int int0 = int_0 << 24 | int_1 << 16 | int_2 << 8;
                return int0;
            }

            public static int smethod_3(int int_0, int int_1)
            {
                int int0 = int_0 << 24 | int_1 >> 2 & 8388607;
                return int0;
            }

            public static int smethod_4(int int_0)
            {
                return int_0 >> 24 & 255;
            }

            public static int smethod_5(int int_0)
            {
                return int_0 >> 16 & 255;
            }

            public static int smethod_6(int int_0)
            {
                return int_0 << 9 >> 7;
            }
        }
    }
#endif
}
