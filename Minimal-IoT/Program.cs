var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<GpioController>(new GpioController(PinNumberingScheme.Board));
builder.Services.AddSingleton<Dht22>(services => new Dht22(pin: 7, gpioController: services.GetRequiredService<GpioController>(), shouldDispose: false));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo { Title = "Minimal IoT" }));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = string.Empty;
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Minimal IoT API v1");
});

app.MapPost("/api/led", (GpioController controller, string color) =>
{
    const int redPinNumber = 36;
    const int greenPinNumber = 37;
    const int bluePinNumber = 38;

    SetPin(redPinNumber, PinValue.Low);
    SetPin(greenPinNumber, PinValue.Low);
    SetPin(bluePinNumber, PinValue.Low);

    int? pin = color?.ToLowerInvariant() switch
    {
        "red" => redPinNumber,
        "green" => greenPinNumber,
        "blue" => bluePinNumber,
        "black" or "blank" or null => null,
        _ => throw new NotSupportedException()
    };

    if (pin.HasValue)
    {
        SetPin(pin.Value, PinValue.High);
    }

    return Results.NoContent();

    void SetPin(int pinNumber, PinValue value)
    {
        if (!controller.IsPinOpen(pinNumber))
        {
            controller.OpenPin(pinNumber, PinMode.Output);
        }

        controller.Write(pinNumber, value);
    }
})
.Produces(StatusCodes.Status204NoContent)
.WithName("SetLedColor");

app.MapGet("/api/humiture", (Dht22 dht22) =>
{
    var temperature = dht22.Temperature;
    var humidity = dht22.Humidity;

    if (dht22.IsLastReadSuccessful)
    {
        return Results.Ok(new
        {
            Temperature = Math.Round(temperature.DegreesCelsius, 2),
            Humidity = Math.Round(humidity.Percent, 2),
            HeatIndex = Math.Round(WeatherHelper.CalculateHeatIndex(temperature, humidity).DegreesCelsius, 2),
            AbsoluteHumidity = Math.Round(WeatherHelper.CalculateAbsoluteHumidity(temperature, humidity).GramsPerCubicMeter, 2)
        });
    }

    return Results.StatusCode(StatusCodes.Status504GatewayTimeout);
})
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status504GatewayTimeout)
.WithName("GetHumiture");

app.Run();