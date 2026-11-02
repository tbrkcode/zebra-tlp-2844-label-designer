// SPDX-License-Identifier: GPL-3.0-only
namespace EtiketStudio.Core;

public static class PrintPreparation
{
    private static readonly string[] ProductTokens = ["{Name}", "{Barcode}", "{Sku}", "{Price}", "{Category}", "{Description}"];
    public static bool RequiresProduct(LabelTemplate template) => template.Elements.Any(e =>
        e.Kind is ElementKind.ProductName or ElementKind.ProductCode or ElementKind.Price ||
        (e.Kind == ElementKind.Image && e.Image is null) ||
        (e.Kind is ElementKind.Text or ElementKind.Barcode or ElementKind.QrCode &&
         ProductTokens.Any(token => (e.Text ?? "").Contains(token, StringComparison.Ordinal))));

    // A missing product must never silently print the editor's demonstration values.
    public static Product ResolveProduct(LabelTemplate template, Product? product)
    {
        if (product is not null) return product.Clone();
        if (RequiresProduct(template)) throw new DomainException("PreviewNeedsProduct");
        return new Product { Name = template.Name };
    }
}
