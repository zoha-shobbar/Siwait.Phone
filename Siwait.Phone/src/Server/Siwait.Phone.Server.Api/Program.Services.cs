﻿using System.Net;
using System.Net.Mail;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Security.Cryptography.X509Certificates;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.OData;
using Microsoft.Net.Http.Headers;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.ResponseCompression;
using Twilio;
using PhoneNumbers;
using FluentStorage;
using FluentStorage.Blobs;
using FluentEmail.Core;
using AdsPush;
using AdsPush.Abstraction;
using Siwait.Phone.Server.Api.Services;
using Siwait.Phone.Server.Api.Controllers;
using Siwait.Phone.Server.Api.Models.Identity;
using Siwait.Phone.Server.Api.Services.Identity;

namespace Siwait.Phone.Server.Api;

public static partial class Program
{
    public static void AddServerApiProjectServices(this WebApplicationBuilder builder)
    {
        // Services being registered here can get injected in server project only.
        var env = builder.Environment;
        var services = builder.Services;
        var configuration = builder.Configuration;
        var appSettings = configuration.Get<ServerApiSettings>()!;

        services.AddScoped<EmailService>();
        services.AddScoped<PhoneService>();
        if (appSettings.Sms?.Configured is true)
        {
            TwilioClient.Init(appSettings.Sms.TwilioAccountSid, appSettings.Sms.TwilioAutoToken);
        }

        services.AddSingleton(_ => PhoneNumberUtil.GetInstance());
        services.AddSingleton<IBlobStorage>(sp =>
        {
            var isRunningInsideDocker = Directory.Exists("/container_volume"); // It's supposed to be a mounted volume named /container_volume
            var attachmentsDirPath = Path.Combine(isRunningInsideDocker ? "/container_volume" : Directory.GetCurrentDirectory(), "App_Data");
            Directory.CreateDirectory(attachmentsDirPath);
            return StorageFactory.Blobs.DirectoryFiles(attachmentsDirPath);
        });
        services.AddSingleton(_ =>
        {
            var adsPushSenderBuilder = new AdsPushSenderBuilder();

            if (string.IsNullOrEmpty(appSettings.AdsPushAPNS?.P8PrivateKey) is false)
            {
                adsPushSenderBuilder = adsPushSenderBuilder.ConfigureApns(appSettings.AdsPushAPNS, null);
            }

            if (string.IsNullOrEmpty(appSettings.AdsPushFirebase?.PrivateKey) is false)
            {
                appSettings.AdsPushFirebase.PrivateKey = appSettings.AdsPushFirebase.PrivateKey.Replace(@"\n", string.Empty);

                adsPushSenderBuilder = adsPushSenderBuilder.ConfigureFirebase(appSettings.AdsPushFirebase, AdsPushTarget.Android);
            }

            if (string.IsNullOrEmpty(appSettings.AdsPushVapid?.PrivateKey) is false)
            {
                adsPushSenderBuilder = adsPushSenderBuilder.ConfigureVapid(appSettings.AdsPushVapid, null);
            }

            return adsPushSenderBuilder
                .BuildSender();
        });
        services.AddScoped<PushNotificationService>();

        services.AddExceptionHandler<ServerExceptionHandler>();

        services.AddResponseCaching();

        services.AddHttpContextAccessor();

        services.AddResponseCompression(opts =>
        {
            opts.EnableForHttps = true;
            opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/octet-stream"]).ToArray();
            opts.Providers.Add<BrotliCompressionProvider>();
            opts.Providers.Add<GzipCompressionProvider>();
        })
            .Configure<BrotliCompressionProviderOptions>(opt => opt.Level = CompressionLevel.Fastest)
            .Configure<GzipCompressionProviderOptions>(opt => opt.Level = CompressionLevel.Fastest);


        services.AddCors(builder =>
        {
            builder.AddDefaultPolicy(policy =>
            {
                if (env.IsDevelopment() is false)
                {
                    policy.SetPreflightMaxAge(TimeSpan.FromDays(1)); // https://stackoverflow.com/a/74184331
                }

                var webClientUrl = configuration.Get<ServerApiSettings>()!.WebClientUrl;

                policy.SetIsOriginAllowed(origin =>
                            AllowedOriginsRegex().IsMatch(origin) ||
                            (string.IsNullOrEmpty(webClientUrl) is false && string.Equals(origin, webClientUrl, StringComparison.InvariantCultureIgnoreCase)))
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .WithExposedHeaders(HeaderNames.RequestId);
            });
        });

        services.AddAntiforgery();

        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.AddRange([AppJsonContext.Default, IdentityJsonContext.Default]));

        services
            .AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.TypeInfoResolverChain.AddRange([AppJsonContext.Default, IdentityJsonContext.Default]))
            .AddApplicationPart(typeof(AppControllerBase).Assembly)
            .AddOData(options => options.EnableQueryFeatures())
            .AddDataAnnotationsLocalization(options => options.DataAnnotationLocalizerProvider = StringLocalizerProvider.ProvideLocalizer)
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    throw new ResourceValidationException(context.ModelState.Select(ms => (ms.Key, ms.Value!.Errors.Select(e => new LocalizedString(e.ErrorMessage, e.ErrorMessage)).ToArray())).ToArray());
                };
            });

        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = env.IsDevelopment();
        });

        services.AddPooledDbContextFactory<AppDbContext>(AddDbContext);
        services.AddDbContextPool<AppDbContext>(AddDbContext);

        void AddDbContext(DbContextOptionsBuilder options)
        {
            options.EnableSensitiveDataLogging(env.IsDevelopment())
                .EnableDetailedErrors(env.IsDevelopment());

            options.UseSqlServer(configuration.GetConnectionString("SqlServerConnectionString"), dbOptions =>
            {

            });
        };

        services.AddOptions<IdentityOptions>()
            .Bind(configuration.GetRequiredSection(nameof(ServerApiSettings.Identity)))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<ServerApiSettings>()
            .Bind(configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(sp => configuration.Get<ServerApiSettings>()!);

        services.AddEndpointsApiExplorer();

        AddSwaggerGen(builder);

        AddIdentity(builder);

        var emailSettings = appSettings.Email ?? throw new InvalidOperationException("Email settings are required.");
        var fluentEmailServiceBuilder = services.AddFluentEmail(emailSettings.DefaultFromEmail);

        if (emailSettings.UseLocalFolderForEmails)
        {
            var isRunningInsideDocker = Directory.Exists("/container_volume"); // It's supposed to be a mounted volume named /container_volume
            var sentEmailsFolderPath = Path.Combine(isRunningInsideDocker ? "/container_volume" : Directory.GetCurrentDirectory(), "App_Data", "sent-emails");

            Directory.CreateDirectory(sentEmailsFolderPath);

            fluentEmailServiceBuilder.AddSmtpSender(() => new SmtpClient
            {
                DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                PickupDirectoryLocation = sentEmailsFolderPath
            });
        }
        else
        {
            if (emailSettings.HasCredential)
            {
                fluentEmailServiceBuilder.AddSmtpSender(() => new(emailSettings.Host, emailSettings.Port)
                {
                    Credentials = new NetworkCredential(emailSettings.UserName, emailSettings.Password),
                    EnableSsl = true
                });
            }
            else
            {
                fluentEmailServiceBuilder.AddSmtpSender(emailSettings.Host, emailSettings.Port);
            }
        }

    }

    private static void AddIdentity(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;
        var env = builder.Environment;
        var appSettings = configuration.Get<ServerApiSettings>()!;
        var identityOptions = appSettings.Identity;

        var certificatePath = Path.Combine(AppContext.BaseDirectory, "DataProtectionCertificate.pfx");
        var certificate = new X509Certificate2(certificatePath, appSettings.DataProtectionCertificatePassword, OperatingSystem.IsWindows() ? X509KeyStorageFlags.EphemeralKeySet : X509KeyStorageFlags.DefaultKeySet);

        services.AddDataProtection()
            .PersistKeysToDbContext<AppDbContext>()
            .ProtectKeysWithCertificate(certificate);


        services.AddIdentity<User, Role>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<AppIdentityErrorDescriber>()
            .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
            .AddApiEndpoints();

        services.AddScoped<IUserConfirmation<User>, AppUserConfirmation>();
        services.AddScoped(sp => (IUserEmailStore<User>)sp.GetRequiredService<IUserStore<User>>());
        services.AddScoped(sp => (IUserPhoneNumberStore<User>)sp.GetRequiredService<IUserStore<User>>());
        services.AddScoped(sp => (AppUserClaimsPrincipalFactory)sp.GetRequiredService<IUserClaimsPrincipalFactory<User>>());

        var authenticationBuilder = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = IdentityConstants.BearerScheme;
            options.DefaultChallengeScheme = IdentityConstants.BearerScheme;
            options.DefaultScheme = IdentityConstants.BearerScheme;
        })
        .AddBearerToken(IdentityConstants.BearerScheme, options =>
        {
            options.BearerTokenExpiration = identityOptions.BearerTokenExpiration;
            options.RefreshTokenExpiration = identityOptions.RefreshTokenExpiration;

            var validationParameters = new TokenValidationParameters
            {
                ClockSkew = TimeSpan.Zero,
                RequireSignedTokens = true,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new X509SecurityKey(certificate),

                RequireExpirationTime = true,
                ValidateLifetime = true,

                ValidateAudience = true,
                ValidAudience = identityOptions.Audience,

                ValidateIssuer = true,
                ValidIssuer = identityOptions.Issuer,

                AuthenticationType = IdentityConstants.BearerScheme
            };

            options.BearerTokenProtector = new AppSecureJwtDataFormat(appSettings, validationParameters);
            options.RefreshTokenProtector = new AppSecureJwtDataFormat(appSettings, validationParameters);

            options.Events = new()
            {
                OnMessageReceived = async context =>
                {
                    // The server accepts the access_token from either the authorization header, the cookie, or the request URL query string
                    context.Token ??= context.Request.Query.ContainsKey("access_token") ? context.Request.Query["access_token"] : context.Request.Cookies["access_token"];
                }
            };
        });

        if (string.IsNullOrEmpty(configuration["Authentication:Google:ClientId"]) is false)
        {
            authenticationBuilder.AddGoogle(options =>
            {
                options.ClientId = configuration["Authentication:Google:ClientId"]!;
                options.ClientSecret = configuration["Authentication:Google:ClientSecret"]!;
                options.SignInScheme = IdentityConstants.ExternalScheme;
            });
        }

        if (string.IsNullOrEmpty(configuration["Authentication:GitHub:ClientId"]) is false)
        {
            authenticationBuilder.AddGitHub(options =>
            {
                options.ClientId = configuration["Authentication:GitHub:ClientId"]!;
                options.ClientSecret = configuration["Authentication:GitHub:ClientSecret"]!;
                options.SignInScheme = IdentityConstants.ExternalScheme;
            });
        }

        if (string.IsNullOrEmpty(configuration["Authentication:Twitter:ConsumerKey"]) is false)
        {
            authenticationBuilder.AddTwitter(options =>
            {
                options.ConsumerKey = configuration["Authentication:Twitter:ConsumerKey"]!;
                options.ConsumerSecret = configuration["Authentication:Twitter:ConsumerSecret"]!;
                options.RetrieveUserDetails = true;
                options.SignInScheme = IdentityConstants.ExternalScheme;
            });
        }

        services.AddAuthorization();
    }

    private static void AddSwaggerGen(WebApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddSwaggerGen(options =>
        {
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Siwait.Phone.Server.Api.xml"), includeControllerXmlComments: true);
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Siwait.Phone.Shared.xml"));

            options.OperationFilter<ODataOperationFilter>();

            options.AddSecurityDefinition("bearerAuth", new()
            {
                Name = "Authorization",
                Description = "Enter the Bearer Authorization string as following: `Bearer Generated-Bearer-Token`",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            options.AddSecurityRequirement(new()
            {
                {
                    new()
                    {
                        Name = "Bearer",
                        In = ParameterLocation.Header,
                        Reference = new OpenApiReference
                        {
                            Id = "Bearer",
                            Type = ReferenceType.SecurityScheme
                        }
                    },
                    []
                }
            });
        });
    }

    /// <summary>
    /// For either Blazor Hybrid web view, localhost, dev tunnels etc in dev environment.
    /// </summary>
    [GeneratedRegex(@"^(http|https|app):\/\/(localhost|0\.0\.0\.0|0\.0\.0\.1|127\.0\.0\.1|.*?devtunnels\.ms|.*?github\.dev)(:\d+)?(\/.*)?$")]
    private static partial Regex AllowedOriginsRegex();
}