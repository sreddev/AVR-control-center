// AVR Control Center
// Copyright (c) 2026 Sergio (@sreddev)
// Licensed under MIT License
// https://github.com/sreddev/AVR-control-center

using System;
using System.Web;
using AvrControlCenter.Views.Pages;
using Xunit;

namespace AvrControlCenter.Tests;

/// <summary>The About card's Support menu must open the right payment pages.</summary>
public class DonateLinkTests
{
    [Fact]
    public void PayPal_link_is_the_donate_page_with_the_coffee_preset()
    {
        var uri = new Uri(SettingsPage.PayPalDonateUrl);
        Assert.Equal("https://www.paypal.com/donate/", uri.GetLeftPart(UriPartial.Path));

        var q = HttpUtility.ParseQueryString(uri.Query);
        Assert.Equal("M3KKGFD9NCZH8", q["business"]);
        Assert.Equal("5", q["amount"]);
        Assert.Equal("0", q["no_recurring"]);
        Assert.Equal("EUR", q["currency_code"]);
        Assert.Equal("Buy me a coffee ☕ Keeps my free apps alive and updated.", q["item_name"]);
    }

    [Fact]
    public void Revolut_link_is_the_personal_page() =>
        Assert.Equal("https://revolut.me/sreddev", SettingsPage.RevolutUrl);
}
