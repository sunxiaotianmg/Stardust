﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Cache;
using XCode.Membership;

namespace Stardust.Data.Monitors
{
    /// <summary>跟踪模式。跟踪所有项，或者新增项不跟踪</summary>
    public enum TraceModes
    {
        /// <summary>跟踪所有项</summary>
        All = 0,

        /// <summary>仅新增跟踪项但不跟踪</summary>
        CreateNew = 1,

        /// <summary>仅跟踪已有项</summary>
        Existing = 2
    }

    /// <summary>应用跟踪器。负责跟踪的应用管理</summary>
    public partial class AppTracer : Entity<AppTracer>
    {
        #region 对象操作
        static AppTracer()
        {
            // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
            //var df = Meta.Factory.AdditionalFields;
            //df.Add(nameof(Period));

            // 过滤器 UserModule、TimeModule、IPModule
            Meta.Modules.Add<UserModule>();
            Meta.Modules.Add<TimeModule>();
            Meta.Modules.Add<IPModule>();

            // 单对象缓存
            var sc = Meta.SingleCache;
            sc.FindSlaveKeyMethod = k => Find(_.Name == k);
            sc.GetSlaveKeyMethod = e => e.Name;
        }

        /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
        /// <param name="isNew">是否插入</param>
        public override void Valid(Boolean isNew)
        {
            // 如果没有脏数据，则不需要进行任何处理
            if (!HasDirty) return;

            // 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框
            if (Name.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Name), "名称不能为空！");

            var len = _.Nodes.Length;
            if (!Nodes.IsNullOrEmpty() && Nodes.Length > len) Nodes = Nodes.Cut(len);

            // 建议先调用基类方法，基类方法会做一些统一处理
            base.Valid(isNew);

            if (isNew)
            {
                if (!Dirtys[nameof(Period)]) Period = 60;
                if (!Dirtys[nameof(MaxSamples)]) MaxSamples = 1;
                if (!Dirtys[nameof(MaxErrors)]) MaxErrors = 10;
                if (!Dirtys[nameof(Timeout)]) Timeout = 5000;
            }
            else
            {
                ItemCount = TraceItems.Count(e => e.Enable);
            }
        }

        /// <summary>已重载。</summary>
        /// <returns></returns>
        public override String ToString() => !DisplayName.IsNullOrEmpty() ? DisplayName : Name;
        #endregion

        #region 扩展属性
        /// <summary>
        /// 跟踪项集合
        /// </summary>
        [XmlIgnore, IgnoreDataMember]
        public IList<TraceItem> TraceItems => Extends.Get(nameof(TraceItems), k => TraceItem.FindAllByApp(ID));
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static AppTracer FindByID(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.ID == id);
        }

        /// <summary>根据编号查找</summary>
        /// <param name="appId">编号</param>
        /// <returns>实体对象</returns>
        public static AppTracer FindByAppId(Int32 appId)
        {
            if (appId < 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.AppId == appId);

            return Find(_.AppId == appId);
        }

        /// <summary>根据名称查找</summary>
        /// <param name="name">名称</param>
        /// <returns>实体对象</returns>
        public static AppTracer FindByName(String name)
        {
            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Name == name);

            // 单对象缓存
            //return Meta.SingleCache.GetItemWithSlaveKey(name) as AppTracer;

            return Find(_.Name == name);
        }
        #endregion

        #region 高级查询
        /// <summary>高级查询</summary>
        /// <param name="category">分类</param>
        /// <param name="enable"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<AppTracer> Search(String category, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (!category.IsNullOrEmpty()) exp &= _.Category == category;
            if (enable != null) exp &= _.Enable == enable.Value;

            exp &= _.UpdateTime.Between(start, end);

            if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.DisplayName.Contains(key) | _.Category.Contains(key) | _.CreateUser.Contains(key) | _.CreateIP.Contains(key) | _.UpdateUser.Contains(key) | _.UpdateIP.Contains(key);

            return FindAll(exp, page);
        }

        // Select Count(ID) as ID,Category From AppTracer Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By ID Desc limit 20
        static readonly FieldCache<AppTracer> _CategoryCache = new FieldCache<AppTracer>(nameof(Category));

        /// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
        /// <returns></returns>
        public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
        #endregion

        #region 业务操作
        /// <summary>添加排除项</summary>
        /// <param name="value"></param>
        public void AddExclude(String value)
        {
            if (value.IsNullOrEmpty()) return;

            var es = new List<String>();
            var ss = Excludes?.Split(",");
            if (ss != null) es.AddRange(ss);

            if (!es.Contains(value))
            {
                es.Add(value);

                Excludes = es.Distinct().Join();
            }
        }

        /// <summary>
        /// 判断指定客户端是否Vip客户端
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns></returns>
        public Boolean IsVip(String clientId)
        {
            if (VipClients.IsNullOrEmpty() || clientId.IsNullOrEmpty()) return false;

            foreach (var item in VipClients.Split(","))
            {
                if (item.IsMatch(item)) return true;
            }

            return false;
        }

        /// <summary>
        /// 获取或添加跟踪项
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public TraceItem GetOrAddItem(String name)
        {
            if (name.IsNullOrEmpty()) return null;

            var list = TraceItems;
            var ti = list.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));
            if (ti != null) return ti;

            // 通过规则匹配，支持把多个埋点聚合到一起
            ti = list.FirstOrDefault(e => e.IsMatch(name));
            if (ti != null) return ti;

            var isApi = name.StartsWith('/');

            // 如果只跟踪已存在埋点，则跳过。仅针对API
            if (Mode == TraceModes.Existing && isApi) return null;

            ti = new TraceItem { AppId = ID, Name = name };
            ti.Enable = Mode == TraceModes.All || !isApi;
            ti.Insert();

            list.Add(ti);

            return ti;
        }

        /// <summary>修正数据</summary>
        public void Fix()
        {
            var list = TraceDayStat.FindAllByAppId(ID);
            //Days = list.DistinctBy(e => e.StatDate.Date).Count();
            Days = list.Select(e => e.StatDate.ToFullString()).Distinct().Count();
            Total = list.Sum(e => e.Total);

            ItemCount = TraceItems.Count(e => e.Enable);
        }
        #endregion
    }
}