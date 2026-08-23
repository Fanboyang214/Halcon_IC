using System;
using System.Collections.Generic;

namespace Core.Models
{
    /// <summary>
    /// 分页查询结果集。
    /// </summary>
    /// <typeparam name="T">记录类型。</typeparam>
    public class PagedResult<T>
    {
        /// <summary>当前页码（从 0 开始）。</summary>
        public int PageIndex { get; set; }

        /// <summary>每页条数。</summary>
        public int PageSize { get; set; }

        /// <summary>符合条件的总记录数。</summary>
        public int TotalCount { get; set; }

        /// <summary>总页数（TotalCount / PageSize 向上取整）。</summary>
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);

        /// <summary>当前页数据。</summary>
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    }
}
