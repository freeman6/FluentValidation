#region License
// Copyright (c) Jeremy Skinner (http://www.jeremyskinner.co.uk)
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
// 
// http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
// 
// The latest version of this file can be found at https://github.com/JeremySkinner/FluentValidation
#endregion

namespace FluentValidation.Internal {
	using System;
	using System.Collections.Generic;
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Linq;
	using System.ComponentModel;
#if !NETSTANDARD1_0
	using System.ComponentModel.DataAnnotations;
#endif
	/// <summary>
	/// Instancace cache.
	/// </summary>
	public class InstanceCache {
		readonly Dictionary<Type, object> cache = new Dictionary<Type, object>();
		readonly object locker = new object();

		/// <summary>
		/// Gets or creates an instance using Activator.CreateInstance
		/// </summary>
		/// <param name="type">The type to instantiate</param>
		/// <returns>The instantiated object</returns>
		public object GetOrCreateInstance(Type type) {
			return GetOrCreateInstance(type, Activator.CreateInstance);
		}

		/// <summary>
		/// Gets or creates an instance using a custom factory
		/// </summary>
		/// <param name="type">The type to instantiate</param>
		/// <param name="factory">The custom factory</param>
		/// <returns>The instantiated object</returns>
		public object GetOrCreateInstance(Type type, Func<Type, object> factory) {
			object existingInstance;

			lock(locker) {
				if (cache.TryGetValue(type, out existingInstance)) {
					return existingInstance;
				}

				var newInstance = factory(type);
				cache[type] = newInstance;
				return newInstance;
			}
		}
	}

	/// <summary>
	/// Member accessor cache.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public static class AccessorCache<T> {
		private static readonly Dictionary<Key, Delegate> _cache = new Dictionary<Key, Delegate>();
		private static readonly object _locker = new object();

		/// <summary>
		/// Gets an accessor func based on an expression
		/// </summary>
		/// <typeparam name="TProperty"></typeparam>
		/// <param name="member">The member represented by the expression</param>
		/// <param name="expression"></param>
		/// <returns>Accessor func</returns>
		public static Func<T, TProperty> GetCachedAccessor<TProperty>(MemberInfo member, Expression<Func<T, TProperty>> expression) {
			if (member == null || ValidatorOptions.DisableAccessorCache) {
				return expression.Compile();
			}
			
 			Delegate result;

			lock (_locker) {
				var key = new Key(member, expression);
				if (_cache.TryGetValue(key, out result)) {
					return (Func<T, TProperty>)result;
				}

				var func = expression.Compile();
				_cache[key] = func;
				return func;
			}
		}

		public static void Clear() {
			lock (_locker) {
				_cache.Clear();
			}
		}

	    private class Key {
	        private readonly MemberInfo memberInfo;
	        private readonly string expressionDebugView;

	        public Key(MemberInfo member, Expression expression) {
	            memberInfo = member;
	            expressionDebugView = expression.ToString();
	        }

	        protected bool Equals(Key other) {
	            return Equals(memberInfo, other.memberInfo) && string.Equals(expressionDebugView, other.expressionDebugView);
	        }

	        public override bool Equals(object obj) {
	            if (ReferenceEquals(null, obj)) return false;
	            if (ReferenceEquals(this, obj)) return true;
	            if (obj.GetType() != this.GetType()) return false;
	            return Equals((Key) obj);
	        }

	        public override int GetHashCode() {
	            unchecked {
	                return ((memberInfo != null ? memberInfo.GetHashCode() : 0)*397) ^ (expressionDebugView != null ? expressionDebugView.GetHashCode() : 0);
	            }
	        }
	    }

	}


	/// <summary>
	/// Display name cache.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	internal static class DisplayNameCache {
		private static readonly Dictionary<MemberInfo, string> _cache = new Dictionary<MemberInfo, string>();
		private static readonly object _locker = new object();

		public static string GetCachedDisplayName(MemberInfo member) {
			string result;

			lock (_locker) {
				if (_cache.TryGetValue(member, out result)) {
					return result;
				}

				string displayName = GetDisplayName(member);
				_cache[member] = displayName;
				return displayName;
			}
		}

		public static void Clear() {
			lock (_locker) {
				_cache.Clear();
			}
		}

#if NETSTANDARD1_0
		// Nasty hack to work around not referencing DataAnnotations directly. 
		// At some point investigate the DataAnnotations reference issue in more detail and go back to using the code above. 
		static string GetDisplayName(MemberInfo member) {
			var attributes = (from attr in member.GetCustomAttributes(true)
				select new { attr, type = attr.GetType() }).ToList();

			string name = (from attr in attributes
				where attr.type.Name == "DisplayAttribute"
				let method = attr.type.GetRuntimeMethod("GetName", new Type[0])
				where method != null
				select method.Invoke(attr.attr, null) as string).FirstOrDefault();

			if (string.IsNullOrEmpty(name)) {
				name = (from attr in attributes
					where attr.type.Name == "DisplayNameAttribute"
					let property = attr.type.GetRuntimeProperty("DisplayName")
					where property != null
					select property.GetValue(attr.attr, null) as string).FirstOrDefault();
			}

			return name;
		}


#else
		static string GetDisplayName(MemberInfo member) {

			if (member == null) return null;
			string name = null;

			var displayAttribute = (DisplayAttribute)Attribute.GetCustomAttribute(member, typeof(DisplayAttribute));

			if (displayAttribute != null) {
				name = displayAttribute.GetName();
			}

			if (string.IsNullOrEmpty(name)) {
				// Couldn't find a name from a DisplayAttribute. Try DisplayNameAttribute instead.
				var displayNameAttribute = (DisplayNameAttribute)Attribute.GetCustomAttribute(member, typeof(DisplayNameAttribute));
				if (displayNameAttribute != null) {
					name = displayNameAttribute.DisplayName;
				}
			}

			return name;

		}
#endif
	}

}