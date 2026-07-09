using System;
using Wpf.Ui.Abstractions;

namespace Lyracist.Services.Display;

public class PageService(IServiceProvider serviceProvider) : INavigationViewPageProvider
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public object? GetPage(Type pageType)
    {
        return _serviceProvider.GetService(pageType);
    }
}
