using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Net.Sockets;
using System.Net.Mail;
using System.Threading.Tasks;

namespace Server
{
    class Program
    {
        protected struct example 
        {
            string exampl;
        };

        protected struct Person
        {
            public string login;
            public string pass;
            public string email;
            public int key;
            public int keyRest;
        };

        protected struct Message
        {
            public string from;
            public string to;
            public string value;
            public string data;
        }

        static void Main(string[] args)
        {
            Console.Write("Enter port: ");
            TcpListener Server = new TcpListener(Convert.ToInt32(Console.ReadLine()));
            Person[] Data = new Person[1];
            Message[] messages = new Message[1];

            //Example

            Data[0].login = "admin";
            Data[0].pass = "admin";

            Server.Start();
            while (true)
            {
                try
                {
                    Console.WriteLine("Waiting for client...");
                    TcpClient client = Server.AcceptTcpClient();
                    
                    Console.WriteLine("Client Acepted! Waiting for data...");
                    NetworkStream stream = client.GetStream();
                    string type = GetData(stream, true);
                    
                    Console.WriteLine("\nRecive data: \nType: " + type + "\n");

                    if (type == "log")
                    {

                        string log = GetData(stream, true);
                        string pass = GetData(stream, false);
                        bool flag = true;

                        for (int i = 0; i < Data.Length; i++)
                        {
                            if (Data[i].login == log)
                            {
                                if (Data[i].pass == pass)
                                {
                                    Data[i].key = new Random().Next(99999999, 999999999);
                                    stream.Write(Encoding.UTF8.GetBytes(Convert.ToString(Data[i].key)), 0, Encoding.UTF8.GetBytes(Convert.ToString(Data[i].key)).Length);
                                    Console.WriteLine("User login: " + log + ", " + pass + ", " + Data[i].email + ", " + Data[i].key);
                                    flag = false;
                                    break;
                                }
                                flag = false;
                                Console.WriteLine("Wrong pass");
                                stream.Write(Encoding.UTF8.GetBytes("{ERR}"), 0, Encoding.UTF8.GetBytes("{ERR}").Length);
                                break;
                            }
                        }
                        if (flag)
                        {
                            Console.WriteLine("Login not found");
                            stream.Write(Encoding.UTF8.GetBytes("{ERR}"), 0, Encoding.UTF8.GetBytes("{ERR}").Length);
                        }
                    }
                    else if (type == "reg")
                    {
                        bool check = true;
                        string log = GetData(stream, true);
                        string pass = GetData(stream, true);
                        string email = GetData(stream, false);

                        for (int i = 0; i < Data.Length; i++)
                        {
                            if (Data[i].login == log)
                            {
                                Console.WriteLine("Entery login");
                                stream.Write(Encoding.UTF8.GetBytes("{ERR}"), 0, Encoding.UTF8.GetBytes("{ERR}").Length);
                                check = false;
                                break;
                            }
                        }

                        if (check)
                        {
                            Person[] temp1 = new Person[Data.Length + 1];

                            for (int i = 0; i < Data.Length; i++)
                            {
                                temp1[i] = Data[i];
                            }

                            temp1[Data.Length].login = log;
                            temp1[Data.Length].pass = pass;
                            temp1[Data.Length].email = email;
                            Data = temp1;
                            stream.Write(Encoding.UTF8.GetBytes("{SYS}"), 0, Encoding.UTF8.GetBytes("{SYS}").Length);
                            Console.WriteLine("New person: " + log + ", " + pass + ", " + email + ".");
                        }
                    }
                    else if (type == "send")
                    {
                        string log = GetData(stream, true);
                        string pass = GetData(stream, true);
                        string messag = GetData(stream, false);

                        Console.WriteLine(log + " " + pass + " " + messag);

                        string from = "";
                        bool check = false;

                        for (int i = 0; i < Data.Length; i++)
                        {
                            if (Data[i].key == Convert.ToInt32(log) && log != "0")
                            {
                                from = Data[i].login;
                                check = true;
                                break;
                            }
                        }

                        if (check)
                        {
                            Message[] tempM = new Message[messages.Length + 1];

                            for (int i = 0; i < messages.Length; i++)
                            {
                                tempM[i] = messages[i];
                            }

                            tempM[messages.Length].from = from;
                            tempM[messages.Length].to = pass;
                            tempM[messages.Length].value = messag;
                            messages = tempM;

                            stream.Write(Encoding.UTF8.GetBytes("{SYS}"), 0, Encoding.UTF8.GetBytes("{SYS}").Length);
                            Console.WriteLine("New message: " + from + " -> " + pass);
                        }
                        else
                        {
                            Console.WriteLine("Err with messag");
                            stream.Write(Encoding.UTF8.GetBytes("{ERR}"), 0, Encoding.UTF8.GetBytes("{ERR}").Length);
                        }
                    }
                    else if (type == "messages")
                    {
                        string log = GetData(stream, false);
                        string result = "";
                        bool newM = false;

                        for (int i = 0; i < Data.Length; i++)
                        {
                            if (Data[i].key == Convert.ToInt32(log) && log != "0")
                            {
                                newM = true;
                                for (int j = 0; j < messages.Length; j++)
                                {
                                    if (messages[j].to == Data[i].login)
                                    {
                                        result += messages[j].from + ":" + messages[j].value + "\n";
                                        Console.WriteLine("Get messages: " + log);
                                    }
                                }
                                break;
                            }
                        }

                        if (newM)
                        {
                            stream.Write(Encoding.UTF8.GetBytes(result), 0, Encoding.UTF8.GetBytes(result).Length);
                        }
                        else
                        {
                            stream.Write(Encoding.UTF8.GetBytes("{ERR}"), 0, Encoding.UTF8.GetBytes("{ERR}").Length);
                            Console.WriteLine("No messages");
                        }
                    }
                    else if (type == "rest")
                    {
                        string login = GetData(stream, true);
                        string email = GetData(stream, false);

                        if (findEmail(Data, email) > 0)
                        {
                            Data[findEmail(Data, email)].keyRest = new Random().Next();
                            string respon = Convert.ToString(Data[findEmail(Data, email)].keyRest);
                            MailMessage messag = new MailMessage("lexa1973f@gmai.com", email, "Rest password!", "Rest Key" + Convert.ToString(Data[findEmail(Data, email)].keyRest));
                            messag.IsBodyHtml = false;
                            SmtpClient smtp = new SmtpClient("stmp.gmail.com", 578);
                            smtp.Credentials = new NetworkCredential("lexa1973f@gmail.com", "7896321451973ex");
                            smtp.EnableSsl = true;
                            smtp.Send(messag);
                            Console.WriteLine("Rest password:" + email + ", " + respon);
                            stream.Write(Encoding.UTF8.GetBytes(respon), 0, Encoding.UTF8.GetBytes(respon).Length);
                        }
                        else
                        {
                            stream.Write(Encoding.UTF8.GetBytes("{ERR}"), 0, Encoding.UTF8.GetBytes("{ERR}").Length);
                        }
                    }
                    else if (type == "conf-rest")
                    {
                        string restKey = GetData(stream, true);
                        string newPass = GetData(stream, false);

                        for (int i = 0; i < Data.Length; i++)
                        {
                            if (Data[i].keyRest == Convert.ToUInt32(restKey) && Convert.ToUInt32(restKey) != 0)
                            {
                                Data[i].pass = newPass;
                                Data[i].keyRest = 0;
                                break;
                            }
                            else
                            {
                                stream.Write(Encoding.UTF8.GetBytes("{ERR}"), 0, Encoding.UTF8.GetBytes("{ERR}").Length);
                                break;
                            }
                        }
                    }
                    else if (type == "exit")
                    {
                        string key = GetData(stream, false);

                        for (int i = 0; i < Data.Length; i++)
                        {
                            if (Data[i].key == Convert.ToInt32(key))
                            {
                                Data[i].key = 0;
                                stream.Write(Encoding.UTF8.GetBytes("{EXT}"), 0, Encoding.UTF8.GetBytes("{EXT}").Length);
                            }
                        }
                    }

                    stream.Close();
                    client.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        static protected int findEmail(Person[] acc, string email)
        {
            for (int i = 0; i < acc.Length; i++)
            {
                if (acc[i].email == email)
                {
                    return i;
                }
            }
            return -1;
        }

        static protected string GetData(NetworkStream stream, bool send)
        {
            string temp = "";

            byte[] vs = new byte[255];
            stream.Read(vs, 0, vs.Length);

            string data = Encoding.UTF8.GetString(vs);

            for (int i = 0; data[i] != '\0'; i++)
            {
                temp += data[i];
            }

            data = temp;
            if (send)
                stream.Write(Encoding.UTF8.GetBytes("{SYS}"), 0, Encoding.UTF8.GetBytes("{SYS}").Length);

            return data;
        }
    }
}