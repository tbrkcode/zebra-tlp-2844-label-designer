// SPDX-License-Identifier: GPL-3.0-only
using EtiketStudio.Core;
using Xunit;
namespace EtiketStudio.Tests;
public sealed class PrintPreparationTests
{
    [Fact] public void StaticLabelNeedsNoCatalogProduct()
    {
        var label = new LabelTemplate { Elements = [new() { Kind = ElementKind.Text, Text = "Hello" }, new() { Kind = ElementKind.Barcode, Text = "123456789012" }] };
        Assert.False(PrintPreparation.RequiresProduct(label));
        var p = PrintPreparation.ResolveProduct(label, null);
        Assert.Equal("", p.Barcode); Assert.Equal("", p.Sku); Assert.Equal(0, p.Price);
    }
    [Theory] [InlineData("{Name}")] [InlineData("{Barcode}")] [InlineData("{Sku}")] [InlineData("{Price}")] [InlineData("{Category}")] [InlineData("{Description}")]
    public void ProductTokensBlockPrintingWithoutProduct(string token)
    {
        var label = new LabelTemplate { Elements = [new() { Kind = ElementKind.Text, Text = "Prefix " + token }] };
        Assert.Equal("PreviewNeedsProduct", Assert.Throws<DomainException>(() => PrintPreparation.ResolveProduct(label, null)).Key);
    }
    [Theory] [InlineData(ElementKind.ProductName)] [InlineData(ElementKind.ProductCode)] [InlineData(ElementKind.Price)] [InlineData(ElementKind.Image)]
    public void ProductFieldsAndUnattachedImagesNeedProduct(ElementKind kind)
    {
        var label = new LabelTemplate { Elements = [new() { Kind = kind }] };
        Assert.True(PrintPreparation.RequiresProduct(label));
    }
    [Fact] public void EmbeddedLogoAndDateDoNotRequireProduct()
    {
        var label = new LabelTemplate { Elements = [new() { Kind = ElementKind.Image, Image = [1] }, new() { Kind = ElementKind.DateTime }, new() { Kind = ElementKind.Text, Text = "{Date} {Time}" }] };
        Assert.False(PrintPreparation.RequiresProduct(label));
    }
    [Fact] public void PrintProductIsAnIndependentSnapshot()
    {
        var p = new Product { Name = "Original", Barcode = "001" }; var snapshot = PrintPreparation.ResolveProduct(LabelTemplate.Default(), p);
        p.Name = "Changed"; Assert.Equal("Original", snapshot.Name); Assert.Equal("001", snapshot.Barcode);
    }
}
