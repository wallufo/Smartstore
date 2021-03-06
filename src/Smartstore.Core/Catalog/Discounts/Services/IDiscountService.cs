﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Smartstore.Core.Identity;

namespace Smartstore.Core.Catalog.Discounts
{
    /// <summary>
    /// Discount service interface.
    /// </summary>
    public partial interface IDiscountService
    {
        /// <summary>
        /// Gets all discounts.
        /// </summary>
        /// <param name="discountType">Discount type. <c>null</c> to load all discounts.</param>
        /// <param name="couponCode">Discount coupon code. <c>null</c> to load all discounts.</param>
        /// <param name="includeHidden">A value indicating whether to include hidden discounts.</param>
        /// <returns>Discounts.</returns>
        Task<IEnumerable<Discount>> GetAllDiscountsAsync(DiscountType? discountType, string couponCode = null, bool includeHidden = false);

        /// <summary>
        /// Checks whether the discount requirements are met.
        /// </summary>
        /// <param name="discount">Discount.</param>
        /// <param name="customer">Customer.</param>
        /// <param name="couponCodeToValidate">Coupon code to validate.</param>
        /// <returns><c>true</c>discount requirements are met, otherwise <c>false</c>.</returns>
        Task<bool> IsDiscountValidAsync(Discount discount, Customer customer, string couponCodeToValidate);
    }
}
