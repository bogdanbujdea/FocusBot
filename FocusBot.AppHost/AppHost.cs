var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.FocusBot_WebAPI>("focusbot-webapi");

builder.Build().Run();
