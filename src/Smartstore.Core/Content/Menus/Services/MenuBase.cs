﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Primitives;
using Smartstore.Collections;
using Smartstore.Diagnostics;
using Smartstore.Utilities;

namespace Smartstore.Core.Content.Menus
{
    public abstract class MenuBase : IMenu
    {
        /// <summary>
        /// Key for Menu caching
        /// </summary>
        /// <remarks>
        /// {0} : Menu name
        /// {1} : Menu specific key suffix
        /// </remarks>
        internal const string MENU_KEY = "pres:menu:{0}-{1}";
        internal const string MENU_PATTERN_KEY = "pres:menu:{0}*";

        private TreeNode<MenuItem> _currentNode;
        private bool _currentNodeResolved;
        private List<string> _providers;

        public ICommonServices Services { get; set; }

        public IMenuPublisher MenuPublisher { get; set; }

        public abstract string Name { get; }

        public virtual bool ApplyPermissions => true;

        public virtual async Task<TreeNode<MenuItem>> GetRootNodeAsync()
        {
            var cacheKey = MENU_KEY.FormatInvariant(Name, GetCacheKey());

            var rootNode = await Services.Cache.GetAsync(cacheKey, async () =>
            {
                using (Services.Chronometer.Step($"Build menu '{Name}'"))
                {
                    var root = await BuildAsync();

                    MenuPublisher.RegisterMenus(root, Name);

                    if (ApplyPermissions)
                    {
                        await DoApplyPermissionsAsync(root);
                    }

                    await Services.EventPublisher.PublishAsync(new MenuBuiltEvent(Name, root));

                    return root;
                }
            });

            return rootNode;
        }

        protected virtual Task DoApplyPermissionsAsync(TreeNode<MenuItem> root)
        {
            // Hide based on permissions
            root.Traverse(async x =>
            {
                if (!await MenuItemAccessPermittedAsync(x.Value))
                {
                    x.Value.Visible = false;
                }
            });

            // Hide dropdown nodes when no child is visible
            root.Traverse(x =>
            {
                var item = x.Value;
                if (!item.IsGroupHeader && !item.HasRoute())
                {
                    if (!x.Children.Any(child => child.Value.Visible))
                    {
                        item.Visible = false;
                    }
                }
            });

            return Task.CompletedTask;
        }

        protected abstract string GetCacheKey();

        protected abstract Task<TreeNode<MenuItem>> BuildAsync();

        public virtual Task ResolveElementCountAsync(TreeNode<MenuItem> curNode, bool deep = false)
        {
            return Task.CompletedTask;
        }

        public virtual async Task<TreeNode<MenuItem>> ResolveCurrentNodeAsync(ActionContext actionContext)
        {
            if (!_currentNodeResolved)
            {
                _currentNode = (await GetRootNodeAsync()).SelectNode(x => x.Value.IsCurrent(actionContext), true);
                _currentNodeResolved = true;
            }

            return _currentNode;
        }

        public IDictionary<string, TreeNode<MenuItem>> GetAllCachedMenus()
        {
            var cache = Services.Cache;
            var keys = cache.Keys(MENU_PATTERN_KEY.FormatInvariant(Name));

            var trees = new Dictionary<string, TreeNode<MenuItem>>(keys.Count());

            foreach (var key in keys)
            {
                var tree = cache.Get<TreeNode<MenuItem>>(key);
                if (tree != null)
                {
                    trees[key] = tree;
                }
            }

            return trees;
        }

        public Task ClearCacheAsync()
        {
            return Services.Cache.RemoveByPatternAsync(MENU_PATTERN_KEY.FormatInvariant(Name));
        }

        #region Utilities

        protected virtual async Task<bool> ContainsProviderAsync(string provider)
        {
            Guard.NotEmpty(provider, nameof(provider));

            if (_providers == null)
            {
                _providers = (await GetRootNodeAsync()).GetMetadata<List<string>>("Providers") ?? new List<string>();
            }

            return _providers.Contains(provider);
        }

        protected virtual T GetRequestValue<T>(ActionContext actionContext, string name)
        {
            Guard.NotNull(actionContext, nameof(actionContext));
            Guard.NotEmpty(name, nameof(name));

            var value = actionContext.RouteData.Values[name]?.ToString();
            if (value.IsEmpty())
            {
                if (!actionContext.HttpContext.Request.Form.TryGetValue(name, out var values))
                {
                    actionContext.HttpContext.Request.Query.TryGetValue(name, out values);
                }
                
                value = values.ToString();
            }

            if (value.HasValue() && CommonHelper.TryConvert<T>(value, out var result))
            {
                return result;
            }

            return default;
        }

        private async Task<bool> MenuItemAccessPermittedAsync(MenuItem item)
        {
            var result = true;

            if (item.PermissionNames.HasValue())
            {
                var permitted = await item
                    .PermissionNames
                    .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .AnyAsync(async x => await Services.Permissions.AuthorizeByAliasAsync(x.Trim()));

                if (!permitted)
                {
                    result = false;
                }
            }

            return result;
        }

        #endregion
    }
}
