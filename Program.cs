using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// 独自に作成したバックグラウンドサービスを登録
builder.Services.AddHostedService<AdsOverMqttTcRxService>();

var app = builder.Build();

// app.MapGet("/", () => "AdsOverMqtt Web Service is running.");

await app.RunAsync();
