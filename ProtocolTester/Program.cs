using ProtocolTester.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddControllers();

// CORS for Postman
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddScoped<IModbusTestService, ModbusTestService>();
builder.Services.AddScoped<IDeviceTypeService, DeviceTypeService>();

var app = builder.Build();
app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();
app.MapControllerRoute(  
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
