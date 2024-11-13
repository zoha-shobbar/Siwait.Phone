﻿using Siwait.Phone.Server.Api.Models.Products;

namespace Siwait.Phone.Server.Api.Models.Categories;

public partial class Category
{
    public Guid Id { get; set; }

    [Required, MaxLength(64)]
    public string? Name { get; set; }

    public string? Color { get; set; }

    public byte[] ConcurrencyStamp { get; set; } = [];

    public IList<Product> Products { get; set; } = [];
}
