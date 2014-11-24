using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

// First Implementation of Interaxon Muse Communication Protocol  > Compresion Funtions 
// Warning: Proof of concept very unoptimized code.
// Ver: 0.1, released: 11/24/2014
// By David Vivancos  > http://www.vivancos.com 

namespace MuseDotNet
{
    class MuseDirect
    {
       public static Boolean debugview = false;


        //Returns 4 10 bits values from 5 bytes
        public static int[] Get10BitNums(byte[] bytes) {
            int[] vals = new int[4];
            Array.Reverse(bytes); // To Litle Endian
            BitArray bits = ToBitArray(bytes);
            for (int c = 0; c <= 3; c++)
            {

                BitArray tmp = new BitArray(32);
                for (int d = 0; d < 10; d++) 
                    tmp[d+22]=bits[c*10+d];
                tmp = BitsReverse(tmp);
                vals[c] = BitConverter.ToInt32(BitArrayToByteArray(tmp).ToArray(), 0);

                if (debugview)
                {
                    for (int d = 0; d < 32; d++)
                    {
                        if (tmp.Get(d))
                            System.Diagnostics.Debug.Write("1");
                        else
                            System.Diagnostics.Debug.Write("0");
                    }
                    System.Diagnostics.Debug.WriteLine("  " + vals[c]);
                }
            }
            return (vals);
        }

        //Gets the number of bits, USE only once sice bytes are reversed
        public static int GetNumbits(byte[] bytes)
        {
            Array.Reverse(bytes); // To Litle Endian
            int i = BitConverter.ToInt16(bytes, 0);
          
            /*int bt = 0;
            bt = i/ 8;
            if((i % 8)>0)
                bt++;

            if (debugview) System.Diagnostics.Debug.WriteLine("lenght:" + i);
            if (debugview) System.Diagnostics.Debug.WriteLine("bytes:" + bt); */
            
            return i;
        }


        //Gets the number of bytes, USE only once sice bytes are reversed
        public static int GetNumBytes(byte[] bytes)  
        {
            Array.Reverse(bytes); // To Litle Endian
            int i = BitConverter.ToInt16(bytes, 0);
            int bt = 0;
            bt = i / 8;
            if ((i % 8) > 0)
                bt++;

            if (debugview) System.Diagnostics.Debug.WriteLine("lenght:" + i);
            if (debugview) System.Diagnostics.Debug.WriteLine("bytes:" + bt);

            return bt;
        }

        //Returns 4 4 bits values from 5 bytes
        public static int[] Get10BitQuants(byte[] bytes)
        {
            int[] vals = new int[4];
                        
            Array.Reverse(bytes); // To Litle Endian
            BitArray bits = ToBitArray(bytes);
            
            for (int c = 0; c <= 3; c++)
            {
                BitArray tmp = new BitArray(32);
                vals[c] = 1;
                for (int d = 0; d < 4; d++) { 
                    tmp[d + 28] = bits[c * 10 + d];
                    
                }
                if (tmp[28])
                    vals[c] *= 16;
                if (tmp[29])
                    vals[c] *= 8;
                if (tmp[30])
                    vals[c] *= 4;
                if (tmp[31])
                    vals[c] *= 2;


                if (debugview) System.Diagnostics.Debug.WriteLine(" QUANTS:  " + vals[c]);  
            }

            return (vals);
        }


        //Returns 4 6 bits values from 5 bytes
        public static int[] Get10BitMedian(byte[] bytes)
        {
            int[] vals = new int[4];

            BitArray bits = ToBitArray(bytes);
            for (int c = 0; c <= 3; c++)
            {

                BitArray tmp = new BitArray(32);
                for (int d = 4; d < 10; d++)
                    tmp[d + 22] = bits[c * 10 + d];
        

                tmp = BitsReverse(tmp);
                vals[c] = BitConverter.ToInt32(BitArrayToByteArray(tmp).ToArray(), 0);

                if (debugview) System.Diagnostics.Debug.WriteLine("  " + vals[c] + " LOG:" + (int)Math.Floor(Math.Log((double)vals[c], 2)));
            }

            return (vals);
        }


        public static int[,] ParsePacket(byte[] bytes,int[] start,int[] median,int[] quantiz) {
            int[,] res = new int[4,16];


            int[] carry = new int[4]; // to keep track of values;
            for(int a=0;a<4;a++){
                carry[a]=start[a];            
            }


            int bitpos = 0;


            Array.Reverse(median);
            Array.Reverse(quantiz);
            Array.Reverse(start);


            BitArray bits = ToBitArray(bytes);


            if (debugview)           //TO print all the bit stream
            {
                for (int d = 0; d < bits.Length; d++)
                {
                    if (bits.Get(d))
                        System.Diagnostics.Debug.Write("1");
                    else
                        System.Diagnostics.Debug.Write("0");
                }
                System.Diagnostics.Debug.WriteLine(" ");
            }

            for (int ch = 0; ch < 4; ch++)
            {
                if(debugview)System.Diagnostics.Debug.WriteLine("CHANNEL:" + ch);
                if (median[ch] == 0)
                {
                    if (debugview) System.Diagnostics.Debug.WriteLine("MEDIAN IS 0");  
                    for (int sm = 0; sm < 16; sm++)
                        res[ch, sm] = start[ch]; 
                    bitpos += (16 * 3);
                }
                else {
                    for (int sm = 0; sm < 16; sm++) {  // 16 samples
                        if (debugview) System.Diagnostics.Debug.WriteLine("CHANNEL:" + ch + " SAMPLE:" + sm);
                        // FIRST QUOTIENT
                        int quotient = 0;
                        while (bits[bitpos]) {  // quotient ends  with a 0 each 1 is 1
                            quotient++;
                            bitpos++;
                        }
                        // Elias Gamma Encoding for >15 quotients  
                        if (quotient == 15)
                        {
                            if (debugview) System.Diagnostics.Debug.WriteLine("ELIAS");
                            int eliasbits = 1;
                            while (!bits[bitpos])
                            {
                                eliasbits++;
                                bitpos++;
                            }
                            BitArray Elias = new BitArray(32);
                            for (int d = 0; d < eliasbits; d++)
                                Elias[d + 32 - eliasbits] = bits[bitpos + d];
                            bitpos += eliasbits;
                            Elias = BitsReverse(Elias);
                            quotient = BitConverter.ToInt32(BitArrayToByteArray(Elias).ToArray(), 0);
                        }
                        else  
                        {
                            bitpos++;
                        }

                        if (debugview) System.Diagnostics.Debug.WriteLine("QUOTIENT:" + quotient + " bit pos:" + bitpos);

                        // SECOND CALCULATE REMAINDER
                        int remainder = 0;
                        int maxreminderbits = (int)Math.Floor(Math.Log((double)median[ch], 2));// (int)(Math.Log(median[ch]) / Math.Log(2));
                        int max1less = (int)Math.Pow(2.0, (double)(maxreminderbits+1)) - median[ch]; // The maximium value for not using an extra bit                        
                        if (debugview) System.Diagnostics.Debug.WriteLine("MAXLESS:" + maxreminderbits + "," + median[ch] + ", " + max1less);

                        BitArray tmp1 = new BitArray(32);
                        for (int d = 0; d < maxreminderbits; d++)
                            tmp1[d + 32 - maxreminderbits] = bits[bitpos + d];

                        tmp1 = BitsReverse(tmp1);
                        remainder = BitConverter.ToInt32(BitArrayToByteArray(tmp1).ToArray(), 0);
                        if (debugview) System.Diagnostics.Debug.WriteLine("TEMPORARY REMINDER:" + remainder + " Bits:" + maxreminderbits);
                        if (remainder >= max1less)  
                        {
                            if (debugview) System.Diagnostics.Debug.WriteLine("NEEDS TO BE 1 BIT MORE!!!");
                            maxreminderbits++;
                            BitArray tmp2 = new BitArray(32);
                            for (int d = 0; d < maxreminderbits; d++)
                                tmp2[d + 32 - maxreminderbits] = bits[bitpos + d];
                            tmp2 = BitsReverse(tmp2);
                            remainder = BitConverter.ToInt32(BitArrayToByteArray(tmp2).ToArray(), 0) - max1less;
                        }
                        if (maxreminderbits == 0)
                            bitpos++;



                        bitpos += maxreminderbits;


                        if (debugview) System.Diagnostics.Debug.WriteLine("REMINDER:" + remainder + " Bits:" + maxreminderbits);

                        // Now the sign
                        int sign = 1;

                        if (!bits[bitpos])
                            sign = -1;
                        if (debugview) System.Diagnostics.Debug.WriteLine("SIGN:" + sign);
                        bitpos++;
                        if (debugview) System.Diagnostics.Debug.WriteLine("BITPOS:" + bitpos);
                        if (debugview) System.Diagnostics.Debug.WriteLine("-----------");

                        res[ch, sm] = carry[ch]+(quotient * median[ch] + remainder) * sign * quantiz[ch];
                        carry[ch] = res[ch, sm];
                    }                
                }



            }

            if (debugview)
            {
                for (int ch = 0; ch < 4; ch++)
                {
                    for (int sm = 0; sm < 16; sm++)
                        System.Diagnostics.Debug.Write(res[ch, sm] + ",");
                    System.Diagnostics.Debug.WriteLine("");
                }
            }
                return res;
        }


	// Auxiliray bit processing functions

        public static BitArray BitsReverse(BitArray bits)
        {
            int len = bits.Count;
            BitArray a = new BitArray(bits);
            BitArray b = new BitArray(bits);

            for (int i = 0, j = len - 1; i < len; ++i, --j)
            {
                a[i] = a[i] ^ b[j];
                b[j] = a[i] ^ b[j];
                a[i] = a[i] ^ b[j];
            }

            return a;
        } 


        public static BitArray ToBitArray(byte[] bytes)        
        {
            BitArray res=new BitArray(bytes.Length*8);
             
             for (int a = 0; a < bytes.Length; a++) {
                 byte b = bytes[a];
                 for (int i = 0; i < 8; i++)  { 
                     res.Set(a * 8 + i,  (b & 0x80) != 0);
                     b *= 2;
                 }
             }
             return res;
            }


        public static int IntFromBits(BitArray bits)
        {
            int res = 0;
            for (int  i = 0; i < 15; ++i)
            {
                if (bits[i])
                {
                    res |= 1 << i;
                }
            }
            return res;
        }
        
        public static byte[] BitArrayToByteArray(BitArray bits)
        {
            
            byte[] ret = new byte[bits.Length / 8];            
            bits.CopyTo(ret, 0);

            if (debugview) { 
                 for (int c = 0; c < 2; c++)   
              System.Diagnostics.Debug.Write(ret[c].ToString("X2"));
            System.Diagnostics.Debug.Write("----");
            }
            return ret;
        }

      

        public static int getIntFromBitArray(BitArray bitArray)
        {

         
            int[] array = new int[1];
            bitArray.CopyTo(array, 0);
            return array[0];

        }
    }

}
