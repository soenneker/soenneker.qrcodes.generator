using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.QrCodes.Generator.Abstract;

namespace Soenneker.QrCodes.Generator.Registrars;

/// <summary>
/// A utility for generating QR codes.
/// </summary>
public static class QrCodeGeneratorRegistrar
{
    /// <summary>
    /// Adds <see cref="IQrCodeGenerator"/> as a singleton service. <para/>
    /// </summary>
    public static IServiceCollection AddQrCodeGeneratorAsSingleton(this IServiceCollection services)
    {
        services.TryAddSingleton<IQrCodeGenerator, QrCodeGenerator>();

        return services;
    }

    /// <summary>
    /// Adds <see cref="IQrCodeGenerator"/> as a scoped service. <para/>
    /// </summary>
    public static IServiceCollection AddQrCodeGeneratorAsScoped(this IServiceCollection services)
    {
        services.TryAddScoped<IQrCodeGenerator, QrCodeGenerator>();

        return services;
    }
}
