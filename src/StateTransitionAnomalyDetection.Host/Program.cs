using StateTransitionAnomalyDetection;
using StateTransitionAnomalyDetection.Adapters.Mocks;
using StateTransitionAnomalyDetection.Host.Endpoints;
using StateTransitionAnomalyDetection.Host.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("UiDevClient", policy =>
        policy.WithOrigins("http://localhost:5173", "http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Singleton is required, not just convenient: each mock adapter holds mutable in-memory
// open-entity state that /reseed mutates in place. Scoped/Transient would silently
// reconstruct fresh, un-reseeded adapters on every request.
builder.Services.AddSingleton<IStateTransitionSource, PrintJobMockAdapter>();
builder.Services.AddSingleton<IStateTransitionSource, SupportTicketMockAdapter>();
builder.Services.AddSingleton<StateDurationBaselineCalculator>();
builder.Services.AddSingleton<AnomalyDetector>();
builder.Services.AddSingleton<IAnomalyExplainer, ClaudeCliAnomalyExplainer>();

var app = builder.Build();

app.UseCors("UiDevClient");
app.UseSwagger();
app.UseSwaggerUI();

app.MapAnomalyEndpoints();

app.Run();
