using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using OpenDis.Core;
using OpenDis.Dis1998;
using OpenDis.Enumerations.EntityState.Appearance;

namespace EspduSender
{
    internal static class EspduSender
    {
        private static IPAddress mcastAddress;
        private static int mcastPort, broadcastPort;
        private static Socket mcastSocket;
        private static MulticastOption mcastOption;
        private static IPEndPoint endPoint;

        private static void MulticastOptionProperties()
        {
            Console.WriteLine("Current multicast group is: " + mcastOption.Group);
            Console.WriteLine("Current multicast local address is: " + mcastOption.LocalAddress);
        }

        private static void JoinMulticast()
        {
            try
            {
                mcastSocket = new Socket(AddressFamily.InterNetwork,
                                         SocketType.Dgram,
                                         ProtocolType.Udp);

                //Console.Write("Enter the local IP address: ");  //In case multiple NICs

                //IPAddress localIPAddr = IPAddress.Parse(Console.ReadLine());
                var localIPAddr = IPAddress.Any;

                var localEP = new IPEndPoint(localIPAddr, 0);  //Don't need to fully join, so can use on same computer as port already in use by receive.

                mcastSocket.Bind(localEP);

                // Define a MulticastOption object specifying the multicast group 
                // address and the local IPAddress.
                // The multicast group address is the same as the address used by the server.
                mcastOption = new MulticastOption(mcastAddress, localIPAddr);

                mcastSocket.SetSocketOption(SocketOptionLevel.IP,
                                            SocketOptionName.AddMembership,
                                            mcastOption);

                endPoint = new IPEndPoint(mcastAddress, mcastPort);

                // Display MulticastOption properties.
                MulticastOptionProperties();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void StartMulticast()
        {
            try
            {
                mcastSocket = new Socket(AddressFamily.InterNetwork,
                                         SocketType.Dgram,
                                         ProtocolType.Udp);

                //Console.Write("Enter the local IP address: ");

                //IPAddress localIPAddr = IPAddress.Parse(Console.ReadLine());
                var localIPAddr = IPAddress.Parse("192.168.0.93");

                //IPAddress localIP = IPAddress.Any;
                var localEP = (EndPoint)new IPEndPoint(localIPAddr, mcastPort);

                mcastSocket.Bind(localEP);

                // Define a MulticastOption object specifying the multicast group 
                // address and the local IPAddress.
                // The multicast group address is the same as the address used by the server.
                mcastOption = new MulticastOption(mcastAddress, localIPAddr);

                mcastSocket.SetSocketOption(SocketOptionLevel.IP,
                                            SocketOptionName.AddMembership,
                                            mcastOption);

                endPoint = new IPEndPoint(mcastAddress, mcastPort);

                // Display MulticastOption properties.
                MulticastOptionProperties();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void StartBroadcast()  //Used to connect to DISMap or other broadcast receivers
        {
            try
            {
                mcastSocket = new Socket(AddressFamily.InterNetwork,
                                         SocketType.Dgram,
                                         ProtocolType.Udp);

                // Define a BroadcastOption object 
                mcastSocket.SetSocketOption(SocketOptionLevel.Socket,
                                            SocketOptionName.Broadcast,
                                            1);
                var localIPAddr = IPAddress.Parse("192.168.0.93");

                endPoint = new IPEndPoint(IPAddress.Broadcast, broadcastPort);
                //endPoint = new IPEndPoint(localIPAddr, broadcastPort);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void SendMessages(byte[] buf)
        {
            try
            {
                //Send multicast packets to the listener.                
                mcastSocket.SendTo(buf, endPoint);

                Console.WriteLine("Sent multicast packets......./n");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public static void Main(string[] args)
        {
            mcastAddress = IPAddress.Parse("239.1.2.3");
            mcastPort = 62040;
            broadcastPort = 62040;  //3000 for DisMapper default
            var espdu = new EntityStatePdu();  //Could use factory but easier this way

            // Med
            double lat = 42.930773;
            double lon = 5.354266;

            // Configure socket.
            JoinMulticast();   //Used to talk to C# receiver.  No need to connect as we are just sending multicast
            //StartMulticast();  //Least preffered
            //StartBroadcast();      //Used for DisMapper      

            //Setup EntityState PDU 
            espdu.ExerciseID = 1;
            var eid = espdu.EntityID;
            eid.Site = 0;
            eid.Application = 1;
            eid.Entity = 2;
            // Set the entity type. SISO has a big list of enumerations, so that by
            // specifying various numbers we can say this is an M1A2 American tank,
            // the USS Enterprise, and so on. We'll make this a tank. There is a 
            // separate project elsehwhere in this project that implements DIS 
            // enumerations in C++ and Java, but to keep things simple we just use
            // numbers here.
            var entityType = espdu.EntityType;
            entityType.EntityKind = 1;      // Platform (vs lifeform, munition, sensor, etc.)
            entityType.Country = 71;              // France
            entityType.Domain = 3;          // Surface (vs air, surface, subsurface, space)
            entityType.Category = 8;        // Mine Countermeasure Ship/Craft
            entityType.Subcategory = 1;
            entityType.Specific = 3;

            // 32-bit int
            SurfacePlatformAppearance spappearance = new SurfacePlatformAppearance()
            {
                State = SurfacePlatformAppearance.StateValue.Deactivated
            };

            espdu.EntityAppearance = spappearance.ToUInt32();

            // Orientation (degrees)
            double heading = 180.0;
            double pitch = 0.0;
            double roll = 0.0;

            // marking
            String marking = "mac12345678";

            // if >11, truncate
            // if <11 fill with 0x00
            int lengthConstraint = espdu.Marking.Characters.Length;
            if (marking.Length > lengthConstraint)
            {
                marking = marking.Substring(0, lengthConstraint);
            }
            else if (marking.Length < lengthConstraint)
            {
                marking = marking.PadRight(lengthConstraint, '\0');
            }

            double circleCenterLatitude = lat;
            double circleCenterLongitude = lon;

            int i = 0;

            while (true)
            {
                espdu.Marking.Characters = System.Text.Encoding.ASCII.GetBytes(marking);

                // make circle with lat/long
                double radius = 0.1; // radius in degrees
                double angle = i * 0.01; // angle in radians
                lat = circleCenterLatitude + radius * Math.Cos(angle);
                lon = circleCenterLongitude + radius * Math.Sin(angle);

                // change heading to tangent circle
                heading = (angle * 180 / Math.PI) + 90;
                if (heading >= 360)
                {
                    heading -= 360;
                }

                double[] disCoordinates = CoordinateConversions.getXYZfromLatLonDegrees(lat, lon, 0.0);

                var location = espdu.EntityLocation;
                location.X = disCoordinates[0];
                location.Y = disCoordinates[1];
                location.Z = disCoordinates[2];

                double[] R = CoordinateConversions.headingPitchRollToEuler(heading, pitch, roll, lat, lon);
                var orientation = espdu.EntityOrientation;
                orientation.Psi = (float)R[0];
                orientation.Theta = (float)R[1];
                orientation.Phi = (float)R[2];

                espdu.Timestamp = DisTime.DisRelativeTimestamp;

                //Prepare output
                var dos = new DataOutputStream(Endian.Big);
                espdu.MarshalAutoLengthSet(dos);

                // Transmit broadcast messages
                SendMessages(dos.ConvertToBytes());
                Console.WriteLine("Message sent with TimeStamp [{0}] Time Of[{1}]", espdu.Timestamp, espdu.Timestamp >> 1);

                Thread.Sleep(200);
                //Console.WriteLine("Hit Enter for Next PDU.  Ctrl-C to Exit");
                //Console.ReadLine();

                i += 1;
            }

            mcastSocket.Close();
        }
    }
}
