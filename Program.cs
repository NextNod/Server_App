using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;

namespace Server
{
    class Program
    {
        static private string dbFileName = "DataBase.db";

        static private SQLiteConnection dbConnector;
        static private SQLiteCommand dbCommand;

        protected struct Person
        {
            public string login;
            public string pass;
            public string email;
            public int key;
            public int keyRest; // для восстановления
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
            Load();
            Connect();

            //while (true)
            //{
            //    try
            //    {
            //        CreateQuery(Console.ReadLine());
            //    }
            //    catch (SQLiteException e)
            //    {
            //        Console.WriteLine(e.Message);
            //    }
            //}

            //Console.Write("Enter port: ");
            TcpListener Server = new TcpListener(111);
            //TcpListener Server = new TcpListener(Convert.ToInt32(Console.ReadLine()));

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
                        //bool flag = true;

                        try
                        {
                            dbCommand.CommandText = $"SELECT login,password FROM users WHERE login='{log}' and password='{pass}';";
                            SQLiteDataReader r = dbCommand.ExecuteReader();
                            r.Read();
                            if (!r.HasRows)
                            {
                                r.Close();
                                string s = "<Server>: No such user or wrong password.";
                                Console.WriteLine(s);
                                stream.Write(Encoding.UTF8.GetBytes("{INF}" + s), 0, Encoding.UTF8.GetBytes("{INF}" + s).Length);
                            }
                            else
                            {
                                Console.WriteLine("OK");

                                r.Close();
                                int key = (new Random().Next(100000000, 1000000000));
                                dbCommand.CommandText = $"UPDATE users SET key='{key.ToString()}' WHERE login='{log}';";
                                dbCommand.ExecuteNonQuery();

                                string s = key.ToString();
                                stream.Write(Encoding.UTF8.GetBytes(s), 0, Encoding.UTF8.GetBytes(s).Length);

                                dbCommand.CommandText = $"SELECT email FROM users WHERE login='{log}';";
                                SQLiteDataReader r2 = dbCommand.ExecuteReader();
                                r2.Read();
                                string email = r2[0].ToString();
                                r2.Close();

                                Console.WriteLine($"User login: {log}, {pass}, {email}, {key}.");
                            }
                        }
                        catch (SQLiteException ex)
                        {
                            Console.WriteLine("<System>: SQLiteException => " + ex.Message);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("<System>: Exception => " + e.Message);
                        }
                    }
                    else if (type == "reg")
                    {
                        string log = GetData(stream, true);
                        string pass = GetData(stream, true);
                        string email = GetData(stream, false);

                        try
                        {
                            dbCommand.CommandText = $"SELECT login FROM users WHERE login='{log}';";
                            SQLiteDataReader r = dbCommand.ExecuteReader();
                            if (r.HasRows)
                            {
                                r.Close();
                                string s = "<Server>: This login is already taken!";
                                Console.WriteLine(s);
                                stream.Write(Encoding.UTF8.GetBytes("{INF}" + s), 0, Encoding.UTF8.GetBytes("{INF}" + s).Length);
                            }
                            else
                            {
                                r.Close();
                                dbCommand.CommandText = $"SELECT email FROM users WHERE login='{email}';";
                                r = dbCommand.ExecuteReader();
                                if (r.HasRows)
                                {
                                    r.Close();
                                    string s = "<Server>: This email is already taken!";
                                    Console.WriteLine(s);
                                    stream.Write(Encoding.UTF8.GetBytes("{INF}" + s), 0, Encoding.UTF8.GetBytes("{INF}" + s).Length);
                                }
                                else
                                {
                                    r.Close();
                                    dbCommand.CommandText = $"INSERT INTO users(login, password, email) VALUES('{log}', '{pass}', '{email}');";
                                    dbCommand.ExecuteNonQuery();
                                    stream.Write(Encoding.UTF8.GetBytes("{INF}Success!"), 0, Encoding.UTF8.GetBytes("{INF}Success!").Length);
                                    Console.WriteLine($"New user: {log}, {pass}, {email}.");
                                }
                            }
                            if (!r.IsClosed)
                                r.Close();
                        }
                        catch (SQLiteException ex)
                        {
                            Console.WriteLine("<System>: SQLiteException => " + ex.Message);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("<System>: Exception => " + e.Message);
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

                        dbCommand.CommandText = $"UPDATE users SET key='0' WHERE key='{key}';";
                        dbCommand.ExecuteNonQuery();
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

        static protected void Load()
        {
            dbCommand = new SQLiteCommand();
            dbConnector = new SQLiteConnection();
        }

        // Устанавливает соединение с БД, если ее нет - создает
        static protected void Connect()
        {
            if (!(File.Exists(dbFileName)))
            {
                SQLiteConnection.CreateFile(dbFileName);
                Console.WriteLine($"<System>: file \"{dbFileName}\" was created.");
            }

            try
            {
                if (dbConnector.State != ConnectionState.Open)
                {
                    dbConnector = new SQLiteConnection($"Data source = {dbFileName};Version=3;");
                    dbConnector.Open();

                    dbCommand.Connection = dbConnector;

                    Console.WriteLine($"<System>: Database \"{dbFileName}\" connected.");
                }
                else
                {
                    Console.WriteLine("<System>: Database already connected!");
                }
            }
            catch (SQLiteException ex)
            {
                Console.WriteLine("<System>: " + ex.Message);
            }
        }

        // Выполняет запрос
        static protected void CreateQuery(string sqlQuery)
        {
            if (dbConnector.State != ConnectionState.Open)
            {
                Console.WriteLine("<System>: Database is not connected!");
                return;
            }

            try
            {
                if (sqlQuery.Contains("SELECT"))
                {
                    dbCommand.CommandText = sqlQuery;
                    SQLiteDataReader r = dbCommand.ExecuteReader();
                    string s = "";
                    while (r.Read())
                    {
                        for (int i = 0; i < r.FieldCount; i++)
                            Console.Write(r[i].ToString() + "|");
                        Console.WriteLine();
                    }
                    r.Close();
                }
                else
                {
                    dbCommand.CommandText = sqlQuery;
                    dbCommand.ExecuteNonQuery();
                }
            }
            catch (SQLiteException ex)
            {
                Console.WriteLine("<System>: " + ex.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine("<System>: Exception " + e.Message);
            }
        }
    }
}