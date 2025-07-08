using MongoDB.Driver;
using SentirseWellApi.Models;

namespace SentirseWellApi.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("MongoDB") ?? 
                                 throw new ArgumentNullException("MongoDB connection string not found");
            
            var mongoClient = new MongoClient(connectionString);
            var databaseName = configuration["MongoDatabase"] ?? "sentirseBien";
            _database = mongoClient.GetDatabase(databaseName);
        }

        public IMongoCollection<User> Users => _database.GetCollection<User>("users");
        public IMongoCollection<Service> Services => _database.GetCollection<Service>("services");
        public IMongoCollection<Turno> Turnos => _database.GetCollection<Turno>("turnos");
        public IMongoCollection<Payment> Payments => _database.GetCollection<Payment>("payments");
        public IMongoCollection<QRCode> QRCodes => _database.GetCollection<QRCode>("qrcodes");

        public IMongoDatabase Database => _database;
    }
} 