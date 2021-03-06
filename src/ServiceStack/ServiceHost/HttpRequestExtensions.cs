using System;
using System.IO;
using System.Net;
using System.Web;
using ServiceStack.Text;
using ServiceStack.Common;
using ServiceStack.Common.Web;
using ServiceStack.WebHost.Endpoints.Extensions;

namespace ServiceStack.ServiceHost
{
	public static class HttpRequestExtensions
	{
		/// <summary>
		/// Gets string value from Items[name] then Cookies[name] if exists.
		/// Useful when *first* setting the users response cookie in the request filter.
		/// To access the value for this initial request you need to set it in Items[].
		/// </summary>
		/// <returns>string value or null if it doesn't exist</returns>
		public static string GetItemOrCookie(this IHttpRequest httpReq, string name)
		{
			object value;
			if (httpReq.Items.TryGetValue(name, out value)) return value.ToString();

			Cookie cookie;
			if (httpReq.Cookies.TryGetValue(name, out cookie)) return cookie.Value;

			return null;
		}

		/// <summary>
		/// Gets request paramater string value by looking in the following order:
		/// - QueryString[name]
		/// - FormData[name]
		/// - Cookies[name]
		/// - Items[name]
		/// </summary>
		/// <returns>string value or null if it doesn't exist</returns>
		public static string GetParam(this IHttpRequest httpReq, string name)
		{
			string value;
			if ((value = httpReq.QueryString[name]) != null) return value;
			if ((value = httpReq.FormData[name]) != null) return value;

			Cookie cookie;
			if (httpReq.Cookies.TryGetValue(name, out cookie)) return cookie.Value;

			object oValue;
			if (httpReq.Items.TryGetValue(name, out oValue)) return oValue.ToString();

			return null;
		}

		public static string GetParentAbsolutePath(this IHttpRequest httpReq)
		{
			return httpReq.GetAbsolutePath().ToParentPath();
		}

		public static string GetAbsolutePath(this IHttpRequest httpReq)
		{
			var resolvedPathInfo = httpReq.PathInfo;

			var pos = httpReq.RawUrl.IndexOf(resolvedPathInfo, StringComparison.InvariantCultureIgnoreCase);
			if (pos == -1)
				throw new ArgumentException(
					String.Format("PathInfo '{0}' is not in Url '{1}'", resolvedPathInfo, httpReq.RawUrl));

			return httpReq.RawUrl.Substring(0, pos + resolvedPathInfo.Length);
		}

		public static string GetParentPathUrl(this IHttpRequest httpReq)
		{
			return httpReq.GetPathUrl().ToParentPath();
		}
		
		public static string GetPathUrl(this IHttpRequest httpReq)
		{
			var resolvedPathInfo = httpReq.PathInfo;

			var pos = resolvedPathInfo == String.Empty
				? httpReq.AbsoluteUri.Length
				: httpReq.AbsoluteUri.IndexOf(resolvedPathInfo, StringComparison.InvariantCultureIgnoreCase);

			if (pos == -1)
				throw new ArgumentException(
					String.Format("PathInfo '{0}' is not in Url '{1}'", resolvedPathInfo, httpReq.RawUrl));

			return httpReq.AbsoluteUri.Substring(0, pos + resolvedPathInfo.Length);
		}

		public static string GetUrlHostName(this IHttpRequest httpReq)
		{
			var aspNetReq = httpReq as HttpRequestWrapper;
			if (aspNetReq != null)
			{
				return aspNetReq.UrlHostName;
			}
			var uri = httpReq.AbsoluteUri;

			var pos = uri.IndexOf("://") + "://".Length;
			var partialUrl = uri.Substring(pos);
			var endPos = partialUrl.IndexOf('/');
			if (endPos == -1) endPos = partialUrl.Length;
			var hostName = partialUrl.Substring(0, endPos).Split(':')[0];
			return hostName;
		}

		public static string GetPhysicalPath(this IHttpRequest httpReq)
		{
			var aspNetReq = httpReq as HttpRequestWrapper;
			if (aspNetReq != null)
			{
				return aspNetReq.Request.PhysicalPath;
			}

			var filePath = httpReq.ApplicationFilePath.CombineWith(httpReq.PathInfo);
			return filePath;
		}

		public static string GetApplicationUrl(this HttpRequest httpReq)
		{
			var appPath = httpReq.ApplicationPath;
			var baseUrl = httpReq.Url.Scheme + "://" + httpReq.Url.Host;
			if (httpReq.Url.Port != 80) baseUrl += ":" + httpReq.Url.Port;
			var appUrl = baseUrl.CombineWith(appPath);
			return appUrl;
		}

		public static string GetHttpMethodOverride(this IHttpRequest httpReq)
		{
			var httpMethod = httpReq.HttpMethod;

			if (httpMethod != HttpMethods.Post)
				return httpMethod;			

			var overrideHttpMethod = 
				httpReq.Headers[HttpHeaders.XHttpMethodOverrideKey].ToNullIfEmpty()
				?? httpReq.FormData[HttpHeaders.XHttpMethodOverrideKey].ToNullIfEmpty()
				?? httpReq.QueryString[HttpHeaders.XHttpMethodOverrideKey].ToNullIfEmpty();

			if (overrideHttpMethod != null)
			{
				if (overrideHttpMethod != HttpMethods.Get && overrideHttpMethod != HttpMethods.Post)
					httpMethod = overrideHttpMethod;
			}

			return httpMethod;
		}

		public static string GetFormatModifier(this IHttpRequest httpReq)
		{
			var format = httpReq.QueryString["format"];
			if (format == null) return null;
			var parts = format.SplitOnFirst('.');
			return parts.Length > 1 ? parts[1] : null;
		}

		public static bool HasNotModifiedSince(this IHttpRequest httpReq, DateTime? dateTime)
		{
			if (!dateTime.HasValue) return false;
			var strHeader = httpReq.Headers[HttpHeaders.IfModifiedSince];
			try
			{
				if (strHeader != null)
				{
					var dateIfModifiedSince = DateTime.ParseExact(strHeader, "r", null);
					var utcFromDate = dateTime.Value.ToUniversalTime();
					//strip ms
					utcFromDate = new DateTime(
						utcFromDate.Ticks - (utcFromDate.Ticks % TimeSpan.TicksPerSecond),
						utcFromDate.Kind
					);

					return utcFromDate <= dateIfModifiedSince;
				}
				return false;
			}
			catch
			{
				return false;
			}
		}

		public static bool DidReturn304NotModified(this IHttpRequest httpReq, DateTime? dateTime, IHttpResponse httpRes)
		{
			if (httpReq.HasNotModifiedSince(dateTime))
			{
				httpRes.StatusCode = (int) HttpStatusCode.NotModified;
				return true;
			}
			return false;
		}
	}
}