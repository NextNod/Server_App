using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Security.Cryptography;
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

                        dbCommand.CommandText = $"SELECT login,password FROM users WHERE login='{log}' and password='{pass}';";
                        SQLiteDataReader r = dbCommand.ExecuteReader();
                        r.Read();
                        if (!r.HasRows)
                        {
                            r.Close();
                            string s = "<Server>: No such user or wrong password.";
                            Console.WriteLine(s);
                            stream.Write(Encoding.UTF8.GetBytes("{ER1}" + s), 0, Encoding.UTF8.GetBytes("{ER1}" + s).Length);
                        }
                        else
                        {
                            r.Close();

                            int key = (new Random().Next(100000000, 1000000000));

                            // проверить нет ли сгенерированного ключа в БД

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
                    else if (type == "reg")
                    {
                        string log = GetData(stream, true);
                        string pass = GetData(stream, true);
                        string email = GetData(stream, false);

                        dbCommand.CommandText = $"SELECT login FROM users WHERE login='{log}';";
                        SQLiteDataReader r = dbCommand.ExecuteReader();
                        if (r.HasRows)
                        {
                            r.Close();
                            string s = "<Server>: This login is already taken!";
                            Console.WriteLine(s);
                            stream.Write(Encoding.UTF8.GetBytes("{ER1}" + s), 0, Encoding.UTF8.GetBytes("{ER1}" + s).Length);
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
                                stream.Write(Encoding.UTF8.GetBytes("{ER1}" + s), 0, Encoding.UTF8.GetBytes("{ER1}" + s).Length);
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
                    else if (type == "send")
                    {
                        string key = GetData(stream, true); // кто отправляет
                        string login = GetData(stream, true); // кому отправлять
                        string messag = GetData(stream, false);

                        Console.WriteLine(login + " " + key + " " + messag);

                        string idSender = GetIdByKey(key);
                        if (idSender != null)
                        {
                            string idReceiver = GetIdByLogin(login);
                            if (idReceiver != null)
                            {
                                // отправить сообщение
                                dbCommand.CommandText = $"INSERT INTO messages(sender, receiver, message) VALUES('{idSender}', '{idReceiver}', '{messag}');";
                                dbCommand.ExecuteNonQuery();
                                Console.WriteLine($"New message: id sender:{idSender} -> id receiver:{idReceiver}");
                                stream.Write(Encoding.UTF8.GetBytes("{INF}"), 0, Encoding.UTF8.GetBytes("{INF}").Length);
                            }
                            // получатель не найден
                            else
                            {
                                string s = "<Server>: Receiver is not founded!";
                                stream.Write(Encoding.UTF8.GetBytes("{ER1}" + s), 0, Encoding.UTF8.GetBytes("{ER1}" + s).Length);
                            }
                        }
                        //  ключ не действителен
                        else
                        {
                            string s = "<Server>: key is not valid!";
                            Console.WriteLine(s);
                            stream.Write(Encoding.UTF8.GetBytes("{ER1}" + s), 0, Encoding.UTF8.GetBytes("{ER1}" + s).Length);
                        }
                    }
                    else if (type == "messages")
                    {
                        string key = GetData(stream, true); // кто отправляет
                        string login = GetData(stream, false); // кому отправлять

                        string idSender = GetIdByLogin(login);
                        if (idSender != null)
                        {
                            string idReceiver = GetIdByKey(key);
                            if (idReceiver != null)
                            {
                                // провеить входящие сообщения
                                dbCommand.CommandText = $"SELECT message FROM messages WHERE sender='{idSender}' AND receiver='{idReceiver}' AND isReaded='0';";
                                SQLiteDataReader r = dbCommand.ExecuteReader();
                                if (r.HasRows)
                                {
                                    string data = "";
                                    while(r.Read())
                                    {
                                        data += "\nMessage:\n" + r[0].ToString() + "\n";
                                        //Console.WriteLine("show: " + r[0].ToString());
                                    }
                                    r.Close();

                                    // помечаем как прочитанное
                                    dbCommand.CommandText = $"UPDATE messages SET isReaded='1' WHERE sender='{idSender}' AND receiver='{idReceiver}' AND isReaded='0';";
                                    dbCommand.ExecuteNonQuery();
                                    stream.Write(Encoding.UTF8.GetBytes("{INF}" + data), 0, Encoding.UTF8.GetBytes("{INF}" + data).Length);
                                }
                                // новых сообщений нет
                                else
                                {
                                    string s = "<Server>: There is no new incoming messages!";
                                    Console.WriteLine(s);
                                    stream.Write(Encoding.UTF8.GetBytes("{ER1}" + s), 0, Encoding.UTF8.GetBytes("{ER1}" + s).Length);
                                }
                            }
                            //  ключ не действителен
                            else
                            {
                                string s = "<Server>: key is not valid!";
                                Console.WriteLine(s);
                                stream.Write(Encoding.UTF8.GetBytes("{ER1}" + s), 0, Encoding.UTF8.GetBytes("{ER1}" + s).Length);
                            }
                        }
                        // отправитель не найден
                        else
                        {
                            string s = "<Server>: Sender is not founded!";
                            Console.WriteLine(s);
                            stream.Write(Encoding.UTF8.GetBytes("{ER1}" + s), 0, Encoding.UTF8.GetBytes("{ER1}" + s).Length);
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
                    else if (type == "add_friend")
                    {
                        // нет ли пользователя уже в друзьях

                        string key = GetData(stream, true); // ключ отправителя
                        string login = GetData(stream, false); // логин получателя

                        // вытянуть id получателя
                        string idReceiver = GetIdByLogin(login);

                        // если данные имеются, то получатель существует
                        if (idReceiver != null) 
                        {
                            // вытянуть id отправителя
                            string idSender = GetIdByKey(key);

                            // если ключ имеется в БД, то отправитель аутетифицирован
                            if (idSender != null)
                            {
                                // не отправлен ли уже исходящий запрос
                                dbCommand.CommandText = $"SELECT * FROM requests WHERE sender='{idSender}' AND receiver='{idReceiver}';";
                                SQLiteDataReader r = dbCommand.ExecuteReader();

                                // если еще не отправлен
                                if (!r.HasRows)
                                {
                                    r.Close();

                                    // не отправлен ли уже входящий запрос
                                    dbCommand.CommandText = $"SELECT * FROM requests WHERE sender = '{idReceiver}' AND receiver = '{idSender}';";
                                    r = dbCommand.ExecuteReader();
                                    //  не отправлен
                                    if (!r.HasRows)
                                    {
                                        r.Close();

                                        // Не являются ли пользователи уже друзьями
                                        dbCommand.CommandText = $"SELECT * FROM friends WHERE (idUser1='{idReceiver}' AND idUser2='{idSender}') OR (idUser2='{idReceiver}' AND idUser1='{idSender}');";
                                        r = dbCommand.ExecuteReader();

                                        // еще не друзья
                                        if (!r.HasRows)
                                        {
                                            r.Close();

                                            // Отправить запрос
                                            dbCommand.CommandText = $"INSERT INTO requests(sender, receiver) VALUES ('{idSender}', '{idReceiver}');";
                                            dbCommand.ExecuteNonQuery();

                                            string s = "<Server>: Success! Request has been sent!";
                                            stream.Write(Encoding.UTF8.GetBytes("{INF}" + s), 0, Encoding.UTF8.GetBytes("{INF}" + s).Length);
                                        }
                                        // уже друзья
                                        else
                                        {
                                            r.Close();
                                            string s = "<Server>: Failed: You are already friends!";
                                            stream.Write(Encoding.UTF8.GetBytes("{ER1}" + s), 0, Encoding.UTF8.GetBytes("{ER1}" + s).Length);
                                        }
                                    }
                                    // отправлен
                                    else
                                    {
                                        r.Close();
                                        string s = "<Server>: Failed: Outgoing request has been already sent!";
                                        stream.Write(Encoding.UTF8.GetBytes("{ER1}" + s), 0, Encoding.UTF8.GetBytes("{ER1}" + s).Length);
                                    }
                                }
                                // если уже отправлен
                                else
                                {
                                    r.Close();
                                    string s = "<Server>: Failed: Incoming request has been already sent!";
                                    stream.Write(Encoding.UTF8.GetBytes("{ER1}" + s), 0, Encoding.UTF8.GetBytes("{ER1}" + s).Length);
                                }
                            }
                            // Если ключа нет в базе
                            else
                            {
                                //
                                stream.Write(Encoding.UTF8.GetBytes("{ER1}"), 0, Encoding.UTF8.GetBytes("{ER1}").Length);
                            }
                        }
                        // получателя нет в БД
                        else
                        {
                            string s = "<Server>: This user is not founded!";
                            Console.WriteLine(s);
                            stream.Write(Encoding.UTF8.GetBytes("{ER1}" + s), 0, Encoding.UTF8.GetBytes("{ER1}" + s).Length);
                        }
                    }
                    else if (type == "check_requests")
                    {
                        string key = GetData(stream, false);

                        string id = GetIdByKey(key);
                        string data = "";

                        // выбрать входящие запросы
                        dbCommand.CommandText = $"SELECT login FROM users JOIN requests ON users.id = requests.sender AND requests.receiver='{id}';";
                        SQLiteDataReader r = dbCommand.ExecuteReader();
                        // если входящие запросы имеются
                        if (r.HasRows)
                        {
                            while(r.Read())
                            {
                                data += r[0].ToString() + "\n";
                            }
                            stream.Write(Encoding.UTF8.GetBytes("{INF}" + data), 0, Encoding.UTF8.GetBytes("{INF}" + data).Length);
                            r.Close();
                        }   
                        // входящий запросов нет
                        else
                        {
                            r.Close();
                            // запросов нет
                            stream.Write(Encoding.UTF8.GetBytes("{ER1}"), 0, Encoding.UTF8.GetBytes("{ER1}").Length);
                        }
                    }
                    // Проверить исходящие запросы на дружбу
                    else if (type == "check_outgoing")
                    {
                        string key = GetData(stream, false);

                        string id = GetIdByKey(key);
                        string data = "";

                        // выбрать входящие запросы
                        dbCommand.CommandText = $"SELECT login FROM users JOIN requests ON users.id = requests.receiver AND requests.sender='{id}';";
                        SQLiteDataReader r = dbCommand.ExecuteReader();
                        // если входящие запросы имеются
                        if (r.HasRows)
                        {
                            while (r.Read())
                            {
                                data += r[0].ToString() + "\n";
                            }
                            stream.Write(Encoding.UTF8.GetBytes("{INF}" + data), 0, Encoding.UTF8.GetBytes("{INF}" + data).Length);
                            r.Close();
                        }
                        // входящий запросов нет
                        else
                        {
                            r.Close();
                            // запросов нет
                            stream.Write(Encoding.UTF8.GetBytes("{ER1}"), 0, Encoding.UTF8.GetBytes("{ER1}").Length);
                        }
                    }
                    else if (type == "accept_request")
                    {
                        string key = GetData(stream, true);
                        string login = GetData(stream, false);

                        string idReceiver = GetIdByKey(key);
                        if (idReceiver != null)
                        {
                            string idSender = GetIdByLogin(login);
                            if (idSender != null)
                            {
                                dbCommand.CommandText = $"SELECT * FROM requests WHERE receiver='{idReceiver}' AND sender='{idSender}';";
                                SQLiteDataReader r = dbCommand.ExecuteReader();

                                if (r.HasRows) // запрос существует
                                {
                                    r.Close();
                                    // удалить запрос в друзья из таблицы
                                    dbCommand.CommandText = $"DELETE FROM requests WHERE receiver='{idReceiver}' AND sender='{idSender}';";
                                    dbCommand.ExecuteNonQuery();
                                    // зарегистрировать дружбу
                                    dbCommand.CommandText = $"INSERT INTO friends(idUser1, idUser2) VALUES('{idSender}', '{idReceiver}');";
                                    dbCommand.ExecuteNonQuery();

                                    string s = "<Server>: Success! Friend has been added!";
                                    stream.Write(Encoding.UTF8.GetBytes("{INF}" + s), 0, Encoding.UTF8.GetBytes("{INF}" + s).Length);
                                }
                                // Данного запроса в друзья не существует
                                else
                                {
                                    r.Close();
                                    string s = "<Server>: Request does not exist!";
                                    stream.Write(Encoding.UTF8.GetBytes("{ER1}" + s), 0, Encoding.UTF8.GetBytes("{ER1}" + s).Length);
                                }
                            }
                            // отправитель не найден в БД
                            else
                            {
                                string s = "<Server>: Sender is not founded!";
                                stream.Write(Encoding.UTF8.GetBytes("{ER1}" + s), 0, Encoding.UTF8.GetBytes("{ER1}" + s).Length);
                            }
                        }
                        // ключ не действителен
                        else
                        {
                            //
                            stream.Write(Encoding.UTF8.GetBytes("{ERR}"), 0, Encoding.UTF8.GetBytes("{ERR}").Length);
                            Console.WriteLine("Key is not valid!");
                        }

                    }
                    // Отклонить входящий запрос
                    else if (type == "decline_incoming")
                    {
                        string key = GetData(stream, true);
                        string login = GetData(stream, false);

                        string idReceiver = GetIdByKey(key);
                        if (idReceiver != null)
                        {
                            string idSender = GetIdByLogin(login);
                            if (idSender != null)
                            {
                                dbCommand.CommandText = $"SELECT * FROM requests WHERE receiver='{idReceiver}' AND sender='{idSender}';";
                                SQLiteDataReader r = dbCommand.ExecuteReader();

                                if (r.HasRows) // запрос существует
                                {
                                    r.Close();
                                    // удалить запрос в друзья из таблицы
                                    dbCommand.CommandText = $"DELETE FROM requests WHERE receiver='{idReceiver}' AND sender='{idSender}';";
                                    dbCommand.ExecuteNonQuery();

                                    string s = "<Server>: Success! Request rejected!";
                                    stream.Write(Encoding.UTF8.GetBytes("{INF}" + s), 0, Encoding.UTF8.GetBytes("{INF}" + s).Length);
                                }
                                // Данного запроса в друзья не существует
                                else
                                {
                                    r.Close();
                                    string s = "<Server>: Request does not exist!";
                                    stream.Write(Encoding.UTF8.GetBytes("{ER1}" + s), 0, Encoding.UTF8.GetBytes("{ER1}" + s).Length);
                                }
                            }
                            // отправитель не найден в БД
                            else
                            {
                                string s = "<Server>: Sender is not founded!";
                                stream.Write(Encoding.UTF8.GetBytes("{ER1}" + s), 0, Encoding.UTF8.GetBytes("{ER1}" + s).Length);
                            }
                        }
                        // ключ не действителен
                        else
                        {
                            stream.Write(Encoding.UTF8.GetBytes("{ERR}"), 0, Encoding.UTF8.GetBytes("{ERR}").Length);
                            Console.WriteLine("Key is not valid!");
                        }
                    }
                    // Отменить исходящий запрос
                    else if (type == "cancel_outgoing")
                    {
                        string key = GetData(stream, true);
                        string login = GetData(stream, false);

                        string idSender = GetIdByKey(key);
                        if (idSender != null)
                        {
                            string idReceiver = GetIdByLogin(login);
                            if (idReceiver != null)
                            {
                                dbCommand.CommandText = $"SELECT * FROM requests WHERE receiver='{idReceiver}' AND sender='{idSender}';";
                                SQLiteDataReader r = dbCommand.ExecuteReader();

                                if (r.HasRows) // запрос существует
                                {
                                    r.Close();
                                    // удалить исходящий запрос из таблицы
                                    dbCommand.CommandText = $"DELETE FROM requests WHERE receiver='{idReceiver}' AND sender='{idSender}';";
                                    dbCommand.ExecuteNonQuery();

                                    string s = "<Server>: Success! Request rejected!";
                                    stream.Write(Encoding.UTF8.GetBytes("{INF}" + s), 0, Encoding.UTF8.GetBytes("{INF}" + s).Length);
                                }
                                // Данного запроса не существует
                                else
                                {
                                    r.Close();
                                    string s = "<Server>: Request does not exist!";
                                    stream.Write(Encoding.UTF8.GetBytes("{ER1}" + s), 0, Encoding.UTF8.GetBytes("{ER1}" + s).Length);
                                }
                            }
                            // получатель не найден в БД
                            else
                            {
                                string s = "<Server>: Receiver is not founded!";
                                stream.Write(Encoding.UTF8.GetBytes("{ER1}" + s), 0, Encoding.UTF8.GetBytes("{ER1}" + s).Length);
                            }
                        }
                        // ключ не действителен
                        else
                        {
                            stream.Write(Encoding.UTF8.GetBytes("{ERR}"), 0, Encoding.UTF8.GetBytes("{ERR}").Length);
                            Console.WriteLine("Key is not valid!");
                        }
                    }
                    // показать друзей
                    else if (type == "show_friends")
                    {
                        string key = GetData(stream, false);
                        string id = GetIdByKey(key);
                        string data = "";

                        // выбрать входящие запросы
                        dbCommand.CommandText = $"SELECT login FROM users JOIN friends ON (users.id = friends.idUser1 AND friends.idUser2='{id}') OR (users.id = friends.idUser2 AND friends.idUser1='{id}');";
                        SQLiteDataReader r = dbCommand.ExecuteReader();
                        // если добавленные друзья есть
                        if (r.HasRows)
                        {
                            while (r.Read())
                            {
                                data += r[0].ToString() + "\n";
                            }
                            stream.Write(Encoding.UTF8.GetBytes("{INF}" + data), 0, Encoding.UTF8.GetBytes("{INF}" + data).Length);
                            r.Close();
                        }
                        // если добавленных друзей нет
                        else
                        {
                            r.Close();
                            stream.Write(Encoding.UTF8.GetBytes("{ER1}"), 0, Encoding.UTF8.GetBytes("{ER1}").Length);
                        }
                    }
                    else if (type == "delete_friend")
                    {
                        string key = GetData(stream, true);
                        string login = GetData(stream, false);

                        string idReceiver = GetIdByKey(key);
                        if (idReceiver != null)
                        {
                            string idSender = GetIdByLogin(login);
                            if (idSender != null)
                            {
                                dbCommand.CommandText = $"SELECT * FROM friends WHERE (idUser1='{idReceiver}' AND idUser2='{idSender}') OR (idUser1='{idSender}' AND idUser2='{idReceiver}');";
                                SQLiteDataReader r = dbCommand.ExecuteReader();

                                if (r.HasRows) // пользователи действительно друзья
                                {
                                    r.Close();
                                    // удалить строку дружбы из таблицы friends
                                    dbCommand.CommandText = $"DELETE FROM friends WHERE (idUser1='{idReceiver}' AND idUser2='{idSender}') OR (idUser1='{idSender}' AND idUser2='{idReceiver}');";
                                    dbCommand.ExecuteNonQuery();

                                    string s = "<Server>: Success! Friend has been deleted!";
                                    stream.Write(Encoding.UTF8.GetBytes("{INF}" + s), 0, Encoding.UTF8.GetBytes("{INF}" + s).Length);
                                }
                                // Данного запроса в друзья не существует
                                else
                                {
                                    r.Close();
                                    string s = "<Server>: You are not friends!";
                                    stream.Write(Encoding.UTF8.GetBytes("{ER1}" + s), 0, Encoding.UTF8.GetBytes("{ER1}" + s).Length);
                                }
                            }
                            // отправитель не найден в БД
                            else
                            {
                                string s = "<Server>: This user is not founded!";
                                stream.Write(Encoding.UTF8.GetBytes("{ER1}" + s), 0, Encoding.UTF8.GetBytes("{ER1}" + s).Length);
                            }
                        }
                        // ключ не действителен
                        else
                        {
                            //
                            stream.Write(Encoding.UTF8.GetBytes("{ERR}"), 0, Encoding.UTF8.GetBytes("{ERR}").Length);
                            Console.WriteLine("Key is not valid!");
                        }
                    }
                    else if (type == "exit")
                    { // добавить проверку на подлиность ключа
                        string key = GetData(stream, false);

                        dbCommand.CommandText = $"UPDATE users SET key='0' WHERE key='{key}';";
                        dbCommand.ExecuteNonQuery();
                    }

                    stream.Close();
                    client.Close();
                }
                catch (SQLiteException ex)
                {
                    Console.WriteLine("<System>: SQLiteException << " + ex.Message);
                }
                catch (Exception e)
                {
                    Console.WriteLine("<System>: Exception << " + e.Message);
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

        static protected string GetIdByKey(string key)
        {
            dbCommand.CommandText = $"SELECT id FROM users WHERE key='{key}';";
            SQLiteDataReader r = dbCommand.ExecuteReader();
            r.Read();
            if (r.HasRows)
            {
                string t = r[0].ToString();
                r.Close();
                return t;
            }
            else
            {
                r.Close();
                return null;
            }
        }
        static protected string GetIdByLogin(string login)
        {
            dbCommand.CommandText = $"SELECT id FROM users WHERE login='{login}';";
            SQLiteDataReader r = dbCommand.ExecuteReader();
            r.Read();
            if (r.HasRows)
            {
                string t = r[0].ToString();
                r.Close();
                return t;
            }
            else
            {
                r.Close();
                return null;
            }
        }
    }
}