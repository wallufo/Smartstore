﻿using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smartstore.Core.Catalog.Products;
using Smartstore.Domain;

namespace Smartstore.Core.Catalog.Categories
{
    public class ProductManufacturerMap : IEntityTypeConfiguration<ProductCategory>
    {
        public void Configure(EntityTypeBuilder<ProductCategory> builder)
        {
            builder.HasQueryFilter(c => !c.Category.Deleted);

            builder.HasOne(c => c.Category)
                .WithMany()
                .HasForeignKey(c => c.CategoryId);

            builder.HasOne(c => c.Product)
                .WithMany(c => c.ProductCategories)
                .HasForeignKey(c => c.ProductId);
        }
    }

    /// <summary>
    /// Represents a product category mapping.
    /// </summary>
    [Table("Product_Category_Mapping")]
    [Index(nameof(IsFeaturedProduct), Name = "IX_IsFeaturedProduct")]
    [Index(nameof(IsSystemMapping), Name = "IX_IsSystemMapping")]
    [Index(nameof(CategoryId), nameof(ProductId), Name = "IX_PCM_Product_and_Category")]
    public partial class ProductCategory : BaseEntity, IDisplayOrder
    {
        private readonly ILazyLoader _lazyLoader;

        public ProductCategory()
        {
        }

        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private member.", Justification = "Required for EF lazy loading")]
        private ProductCategory(ILazyLoader lazyLoader)
        {
            _lazyLoader = lazyLoader;
        }

        /// <summary>
        /// Gets or sets the category identifier.
        /// </summary>
        public int CategoryId { get; set; }

        private Category _category;
        /// <summary>
        /// Gets or sets the category.
        /// </summary>
        public Category Category
        {
            get => _lazyLoader?.Load(this, ref _category) ?? _category;
            set => _category = value;
        }

        /// <summary>
        /// Gets or sets the product identifier.
        /// </summary>
        public int ProductId { get; set; }

        private Product _product;
        /// <summary>
        /// Gets or sets the product.
        /// </summary>
        public Product Product
        {
            get => _lazyLoader?.Load(this, ref _product) ?? _product;
            set => _product = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the product is featured.
        /// </summary>
        public bool IsFeaturedProduct { get; set; }

        /// <summary>
        /// Gets or sets the display order.
        /// </summary>
        public int DisplayOrder { get; set; }

        /// <summary>
        /// Indicates whether the mapping is created by the user or by the system.
        /// </summary>
        public bool IsSystemMapping { get; set; }
    }
}