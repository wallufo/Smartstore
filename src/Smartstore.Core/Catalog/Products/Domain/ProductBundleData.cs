﻿using System;

namespace Smartstore.Core.Catalog.Products
{
    /// <summary>
    /// Encapsulates data of a product bundle item.
    /// </summary>
    public partial class ProductBundleItemData
    {
        public ProductBundleItemData(ProductBundleItem item)
        {
            Item = item;
        }

        /// <summary>
        /// Gets the bundle item. Can be <c>null</c>.
        /// </summary>
        public ProductBundleItem Item { get; private set; }
        public decimal AdditionalCharge { get; set; }
    }

    /// <summary>
    /// Contains the deserialised bundle item data of an ordered product.
    /// </summary>
    [Serializable]
    public partial class ProductBundleItemOrderData
    {
        public int BundleItemId { get; set; }
        public int ProductId { get; set; }
        public string Sku { get; set; }
        public string ProductName { get; set; }
        public string ProductSeName { get; set; }
        public bool VisibleIndividually { get; set; }
        public int Quantity { get; set; }
        public decimal PriceWithDiscount { get; set; }
        public int DisplayOrder { get; set; }
        public string AttributesXml { get; set; }
        public string AttributesInfo { get; set; }
        public bool PerItemShoppingCart { get; set; }
    }
}