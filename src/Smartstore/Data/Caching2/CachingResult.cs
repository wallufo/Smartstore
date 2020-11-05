﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Dasync.Collections;

namespace Smartstore.Data.Caching2
{
    public class CachingResult<TResult, TEntity> : CachingResult<TResult>
    {
        public CachingResult(Expression expression, CachingExpressionVisitor<TResult> visitor)
            : base(expression, visitor)
        {
        }

        public override TResult WrapAsyncResult(object cachedValue)
        {
            object wrappedResult = Visitor.SequenceType == null
                ? Task.FromResult((TEntity)cachedValue)
                : ((IEnumerable<TEntity>)cachedValue ?? Enumerable.Empty<TEntity>()).ToAsyncEnumerable();

            return (TResult)wrappedResult;
        }

        public override object ConvertQueryResult(TResult queryResult)
        {
            return Visitor.SequenceType == null
                ? queryResult
                : ((IEnumerable<TEntity>)queryResult).ToList();
        }

        public override async Task<object> ConvertQueryAsyncResult(TResult queryResult)
        {
            object result = queryResult;
            
            return Visitor.SequenceType == null
                ? await ((Task<TEntity>)result)
                : await ((IAsyncEnumerable<TEntity>)result).ToListAsync();
        }
    }

    /// <summary>
    /// Cached result data container.
    /// </summary>
    public class CachingResult<TResult>
    {
        public CachingResult(Expression expression, CachingExpressionVisitor<TResult> visitor)
        {
            Expression = expression;
            Visitor = visitor;
        }

        /// <summary>
        /// Wraps the cached result for async EF call.
        /// </summary>
        /// <returns>Result for EF.</returns>
        public virtual TResult WrapAsyncResult(object cachedValue) 
            => throw new NotImplementedException();

        /// <summary>
        /// Converts the EF query result to be cacheable.
        /// </summary>
        public virtual object ConvertQueryResult(TResult queryResult) 
            => throw new NotImplementedException();

        /// <summary>
        /// Converts the async EF query result to be cacheable.
        /// </summary>
        public virtual Task<object> ConvertQueryAsyncResult(TResult queryResult)
            => throw new NotImplementedException();

        /// <summary>
        /// The visited expression.
        /// </summary>
        public Expression Expression { get; }

        /// <summary>
        /// The visitor used to resolve policy and types.
        /// </summary>
        public CachingExpressionVisitor<TResult> Visitor { get; }

        /// <summary>
        /// The cache key
        /// </summary>
        public DbCacheKey CacheKey { set; get; }

        /// <summary>
        ///  Retrieved cache entry
        /// </summary>
        public DbCacheEntry CacheEntry { set; get; }

        /// <summary>
        /// Strongly typed value from cache.
        /// </summary>
        public TResult CachedValue => (TResult)CacheEntry?.Value ?? default;

        /// <summary>
        /// The resolved caching policy
        /// </summary>
        public DbCachingPolicy Policy => Visitor.CachingPolicy;

        /// <summary>
        /// The handled entity type
        /// </summary>
        public Type EntityType => Visitor.EntityType;

        /// <summary>
        /// The handled sequence type or <c>null</c> if single result.
        /// </summary>
        public Type SequenceType => Visitor.SequenceType;

        /// <summary>
        /// Could read cached entry from cache?
        /// </summary>
        public bool HasResult => CacheEntry != null;

        /// <summary>
        /// Can result be put to cache?
        /// </summary>
        public bool CanPut => CacheKey != null && Policy != null;
    }
}