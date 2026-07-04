using System;
using Wpf.Ui.Abstractions;

namespace Lyracist.Services.Display;

public class PageService : INavigationViewPageProvider
{
    private readonly IServiceProvider _serviceProvider;

    public PageService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public object? GetPage(Type pageType)
    {
        return _serviceProvider.GetService(pageType);
    }
}
