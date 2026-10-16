var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();


public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}