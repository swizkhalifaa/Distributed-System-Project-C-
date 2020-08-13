using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNet.SignalR;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace SignalRChat
{
    public class ChatHub : Hub
    {       
        // Diconnection method which references the client and redirects client to login
        public override Task OnDisconnected(bool stopCalled)
        {           
            Clients.Caller.disconnected();
            return base.OnDisconnected(stopCalled);
        }       

        // Load method which references MongoDB database and loads lists of messages and active session users
        public void Load()
        {           
            MongoCRUD database = new MongoCRUD("UserBase");          
            var messageList = database.LoadRecords<MessageModel>("Messages");
            foreach (var MessageModel in messageList)
            {
                Clients.Caller.broadcastMessage(MessageModel.usermodel.Username, MessageModel.Message);
            }
            var sessionlist = database.LoadRecords<SessionModel>("Sessions");
            foreach (var SessionModel in sessionlist)
            {
                Clients.Caller.broadcastActiveUser(SessionModel.usermodel.Username);
            }
        }
       
        // Clear method which deletes all sessions
        public void Clear()
        {
            MongoCRUD database = new MongoCRUD("UserBase");
            database.DeleteRecords<SessionModel>("Sessions");
        }

        // Send method which identifies the sender and stores messages within MongoDB and broadcasts messages to all active clients
        public void Send(string sessionId, string message)
        {
            if (message != "")
            {
                MongoCRUD database = new MongoCRUD("UserBase");
                var thisUser = database.LoadRecordById<SessionModel>("Sessions", sessionId);
                var username = thisUser.usermodel.Username;
                MessageModel messagemodel = new MessageModel { Message = message, usermodel = thisUser.usermodel, MessageTime = new BsonDateTime(DateTime.Now) };
                database.InsertRecord("Messages", messagemodel);
                // Call the broadcastMessage method to update clients
                Clients.All.broadcastMessage(username, message);
            }                     
        }

        // Login method with authentication and storage of user within MongoDB
        public string Login(string username, string password)
        {
            UserModel usermodel = new UserModel { Username = username, Password = password };
            
            if (usermodel.Username == "" || usermodel.Password == "")
            {
                Console.WriteLine("empty fields");              
                return "false";
            }
            else
            {
                // Creating session upon login success
                MongoCRUD database = new MongoCRUD("UserBase");
                SessionModel sessionmodel = new SessionModel { Id = Context.ConnectionId, usermodel = usermodel};
                var userList = database.LoadRecords<UserModel>("Users");
                foreach (var UserModel in userList)
                {
                    if(usermodel.Username == UserModel.Username)
                    {
                        if(usermodel.Password == UserModel.Password)
                        {                           
                            database.InsertRecord("Sessions", sessionmodel);
                            Clients.All.RefreshPage();
                            return sessionmodel.Id.ToString();
                        }
                        else
                        {
                            return "false";
                        }
                    }  
                }
                database.InsertRecord("Users", usermodel);
                database.InsertRecord("Sessions", sessionmodel);
                Clients.All.RefreshPage();
                return sessionmodel.Id.ToString();
            }
        }

        // Logout method which removes relevant sessions
        public void Logout(string sessionId)
        {
            MongoCRUD database = new MongoCRUD("UserBase");
            var sessionList = database.LoadRecords<SessionModel>("Sessions");
            foreach (var SessionModel in sessionList)
            {
                if (sessionId == SessionModel.Id.ToString())
                {
                    database.DeleteRecordById<SessionModel>("Sessions", SessionModel.Id.ToString());
                    Clients.All.RefreshPage();
                }                
            }
        }
    }    

    public class MongoCRUD
    {
        // MongoDB instance created
        readonly IMongoDatabase db;

        public MongoCRUD(string database)
        {
            // Connection to relevant MongoDB cluster
            var client = new MongoClient("########");
            // Variable for database reference
            db = client.GetDatabase(database);
        }

        // Inserting records
        public void InsertRecord<T>(string table, T record)
        {
            var collection = db.GetCollection<T>(table);
            collection.InsertOne(record);
        }

        // Load records
        public List<T> LoadRecords<T>(string table)
        {
            var collection = db.GetCollection<T>(table);
            return collection.Find(new BsonDocument()).ToList();
        }

        // Delete records
        public void DeleteRecords<T>(string table)
        {
            db.DropCollection(table);
        }

        // Delete specific record based on id
        public void DeleteRecordById<T>(string table, string id)
        {
            var collection = db.GetCollection<T>(table);

            var filter = Builders<T>.Filter.Eq("Id", id);

            collection.DeleteOne(filter);
        }

        // Load record based on id
        public T LoadRecordById<T>(string table, string id)
        {
            var collection = db.GetCollection<T>(table);

            var filter = Builders<T>.Filter.Eq("Id", id);

            return collection.Find(filter).First();
        }
    }

    // User model
    public class UserModel
    {
        [BsonId] // id
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }        
    }

    // Message model
    public class MessageModel
    {
        [BsonId] // id
        public Guid Id { get; set; }
        public string Message { get; set; }
        public UserModel usermodel { get; set; }
        public BsonDateTime MessageTime { get; set; }
    }

    // Session model
    public class SessionModel
    {
        [BsonId]
        public string Id { get; set; }
        public UserModel usermodel { get; set; }    
    }

}
