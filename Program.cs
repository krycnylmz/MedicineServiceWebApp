using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.


var connectionString = builder.Configuration.GetConnectionString("myConnectionString")
                       ?? throw new InvalidOperationException("Connection string 'myConnectionString' not found.");

// Cosmos DB için DbContext'i yapılandır
builder.Services.AddDbContext<MyContext>(options =>
    options.UseCosmos(
        connectionString,
        "medicinesDb", // Gerçek veritabanı adınızla değiştirin
        options =>
        {
            options.ConnectionMode(ConnectionMode.Gateway); // Use Gateway connection mode
            options.Region(Regions.WestUS); // Replace with your preferred region
        }
    )
);


builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();



var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
//}



app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

