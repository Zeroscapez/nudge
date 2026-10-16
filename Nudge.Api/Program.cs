using Microsoft.EntityFrameworkCore;
using Nudge.Api.Data;
using Nudge.Api.Endpoints;
using Nudge.Api.Services;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<INudgeService, NudgeService>();
builder.Services.AddDbContext<NudgeDbContext>(o => o.UseSqlite("Data Source=nudge.db")); // this is the connection string
builder.Services.AddScoped<IUserService, UserService>();
var app = builder.Build();

app.MapGet("/", () => "Hello World!");
app.MapUserEndpoints(); // this is where we register the user endpoints

app.Run();



