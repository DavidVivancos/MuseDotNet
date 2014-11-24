using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Threading;

// First Implementation of Interaxon Muse Communication Protocol  > Bluetooth COMM
// Warning: Proof of concept unoptimized code
// Ver: 0.1, released: 11/24/2014
// By David Vivancos  > http://www.vivancos.com 

namespace MuseDotNet
{
    class BluetoothMuse
    {
        public static SerialPort BluetoothConnection = new SerialPort();        
        public static int BUFFSIZE = 2048;
        public static byte[] buffer = new byte[BUFFSIZE];
        public static Boolean debugview = false;
        public static int pos = 0, posAct=0;        
        public static int[] lastEEG= new int[4];
        public static int[] quantiz= new int[4];
        public static int[] median= new int[4];
        public static int bits = 0;
        public static int NumBytes = 0;
        public static bool processing = false;
        public static bool recording = false;
        public static Boolean GetC0=false;
        public static bool foundE0 = false;
	public static bool foundE0Again = false;
	public static int C0count = 0;
        public static DispatcherTimer keeptimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(8000) };


        //Init & Start Muse in preset 10
        public static void BluetoothMuse() {


            Boolean portfound = false;

            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                if (port.Equals("COM16"))     // Replace this COM port by the appropriate one on your computer
                    portfound = true;
            }

            if (!portfound)
                return;

        
            BluetoothConnection.PortName = "COM16"; // Replace this COM port by the appropriate one on your computer
            

            BluetoothConnection.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);

            try
            {
                BluetoothConnection.Open();
                BluetoothConnection.ReadTimeout = 1500;
            }
            catch (Exception) {
                return;            
            }
            Byte[] byteCmd;
            byteCmd = new Byte[] { 0x76, 0x20, 0x32, 0x0d };  // L !    
	    sendit(byteCmd);
	          byteCmd = new Byte[] { 0x25, 0x20 }; // % (esp)  
            sendit(byteCmd);
            byteCmd = new Byte[] { 0x31, 0x30 };  // preset 0x31,0x30 = 10 /ASCII,  31 32 for 12 ....
            sendit(byteCmd);
            byteCmd = new Byte[] { 0x0d }; //
            sendit(byteCmd);
            byteCmd = new Byte[] { 0x73, 0x20, 0x35, 0x0d }; // s 5  (begin transmision)
            sendit(byteCmd);

	    // Keep Alive signal for Muse otherwise it will disconnect
            keeptimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(8000) };
            keeptimer.Tick += new EventHandler(keepAlive);
            keeptimer.Start();
            
        
        }

	//Processing Data Loop, 
        private static void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            
            SerialPort sp = (SerialPort)sender;
                        
            if (recording)
            {
                int old = pos;
                int length = sp.BytesToRead;
                // retrieve the reply data 
                for (int i = 0; i < length; i++)
                {
                    buffer[pos] = (byte)sp.ReadByte();
                    pos++;

                }

                if (pos > (old + 4) && !processing)  // avoid processing smaller chunks
                { 
                    processStream();
                }
            }
            else { 
                // retrieve all the data to clear
               sp.ReadExisting();             
            
            }
            
        }

        //Processing bytestream Loop, 
        private static void processStream() {
            processing = true;
            // Look for FFFFAA55
            if (!foundE0) {
                for (int a = posAct; a < pos-5; a++) {
                    if(buffer[a]==0xFF)
                        if(buffer[a+1]==0xFF)
                            if(buffer[a+2]==0xAA)
                                if(buffer[a+3]==0x55)
                                    if (buffer[a+4] == 0xE0) {
                                        posAct = a + 5;
                                        if ((posAct + 5) < pos) {                                            
                                            foundE0 = true;
                                            if (debugview) Debug.Print("FOUND E0");
                                            processE0();
                                            break;
                                        }
                                        else
                                        {
                                            if (debugview) Debug.Print("FOUND E0 but not enough");
                                            posAct = a;
                                            break;

                                        }
                                    }
                    posAct++;
                }                            
            }
            else {
                cleanFF();
                if (C0count < 8) //wait for 8 c0s
                {
                    processC0();
                }
                else {
                    if (debugview) System.Diagnostics.Debug.WriteLine("8 C0s processed");
                    if (debugview) System.Diagnostics.Debug.WriteLine("Buffer position:"+pos);
                    for (int a = posAct; a < pos - 5; a++) {
                        if (buffer[a] == 0xE0) {
                            posAct++;
                            processE0();
                            C0count = 0;
                            a = pos;
                        }
                    }                    
                }
            }           
            processing = false;
        }


	// Remove from the Buffer the FFFFAA55 unused bytes
        private static void cleanFF() {
            for(int a=0;a<pos-4;a++){
                   if(buffer[a]==0xFF)
                        if(buffer[a+1]==0xFF)
                            if(buffer[a+2]==0xAA)
                                if (buffer[a + 3] == 0x55)    {
                                    byte[] buf2 = new byte[BUFFSIZE];                                    
                                    Buffer.BlockCopy(buffer, 0, buf2, 0, a);
                                    Buffer.BlockCopy(buffer, a+4, buf2, a, (pos-a-4));
                                    Buffer.BlockCopy(buf2, 0, buffer, 0, BUFFSIZE);
                                    pos -= 4;                                   
                                }

            }           
        
        }

	//Get the E0 uncompressed EEG Packets
        private static void processE0() {
            
            if (debugview)
            {
                System.Diagnostics.Debug.WriteLine("AfterClean:"); for (int b = 0; b < pos; b++) System.Diagnostics.Debug.Write(buffer[b].ToString("X2") + " ");
            }
            byte[] EEG = new byte[5];
            for (int a = 0; a < 5; a++) {
                EEG[a] = buffer[posAct + a];
            }
            lastEEG = MuseDirect.Get10BitNums(EEG);
            byte[] buf2 = new byte[BUFFSIZE];
            posAct += 5;
            Buffer.BlockCopy(buffer, posAct, buf2, 0, (pos-posAct));
            Buffer.BlockCopy(buf2, 0, buffer, 0, BUFFSIZE);
            pos = pos - posAct;
            posAct = 0;                    
        }


        //Get the C0 Compressed EEG Packets
        private static void processC0() {
            
            if (buffer[posAct] == 0xC0 && pos>(posAct+8)&&!GetC0) {
                if (debugview) { System.Diagnostics.Debug.WriteLine("Full:"); for (int b = posAct; b < pos; b++) System.Diagnostics.Debug.Write(buffer[b].ToString("X2") + " "); }
                
                byte[] cc=new byte[5];
                for (int a = 0; a < 5; a++)
                    cc[a] = buffer[posAct + 1 + a];
                if (debugview) { System.Diagnostics.Debug.WriteLine("cc:"); for (int b = 0; b < 5; b++) System.Diagnostics.Debug.Write(cc[b].ToString("X2") + " "); }
                

                quantiz = MuseDirect.Get10BitQuants(cc);
                median = MuseDirect.Get10BitMedian(cc);

                 byte[] ll = new byte[2];
                 ll[0]=buffer[posAct + 6];
                 ll[1] = buffer[posAct + 7];
                 if (debugview) { System.Diagnostics.Debug.WriteLine("ll:"); for (int b = 0; b < 2; b++) System.Diagnostics.Debug.Write(ll[b].ToString("X2") + " "); }
                 bits = MuseDirect.GetNumbits(ll);
                 int bt = 0;
                 bt = bits / 8;
                 if ((bits % 8) > 0)
                     bt++;
                 NumBytes = bt;
                 posAct = posAct + 8;
                 GetC0 = true;
            }
            if (GetC0 && pos>(NumBytes+posAct)) {
                byte[] bts = new byte[NumBytes];
                for (int a = 0; a < NumBytes; a++)
                    bts[a] = buffer[posAct + a];
                posAct += NumBytes;
                if (debugview) { System.Diagnostics.Debug.WriteLine("bts:"); for (int b = 0; b < NumBytes; b++) System.Diagnostics.Debug.Write(bts[b].ToString("X2") + " "); }
                
                int[,] res = MuseDirect.ParsePacket(bts, lastEEG, median, quantiz);
                
                // Do here what ever you want in your code with the packets in the array res                
                
                for (int a = 0; a < 4; a++)
                    lastEEG[a] = res[a,15];
                C0count++;
                GetC0 = false;
                byte[] buf2 = new byte[BUFFSIZE];
                Buffer.BlockCopy(buffer, posAct, buf2, 0, (pos - posAct));
                Buffer.BlockCopy(buf2, 0, buffer, 0, BUFFSIZE);             
                pos = pos - posAct;
                posAct = 0;           
            }
            

        
        }


	//Send some bytes to the Bluetooth connection
        private static void sendit( Byte[] Command)
        {            
            if (BluetoothConnection != null)
               BluetoothConnection.Write(Command, 0, Command.Length);
            
        }

        


	// Keep Alive Dispatcher
        private static void keepAlive(object sender, EventArgs e)
        {
            Byte[] byteCmd = new Byte[] { 0x6b, 0x0d }; //k  keep alive signal
            sendit(byteCmd);            

        }
       

    }
}
