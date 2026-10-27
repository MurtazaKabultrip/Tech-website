using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Novaforge.Web.Content;
using Novaforge.Web.Services;

namespace Novaforge.Web.Endpoints;

public static class SeoEndpoints
{
    public static IEndpointRouteBuilder MapSeoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/robots.txt", (IOptions<SiteOptions> site) =>
            TypedResults.Text(
                $"User-agent: *\nAllow: /\nDisallow: /portal\nDisallow: /admin\nDisallow: /account\nDisallow: /api\n\nSitemap: {site.Value.BaseUrl.TrimEnd('/')}/sitemap.xml\n",
                "text/plain"));

        app.MapGet("/sitemap.xml", (IOptions<SiteOptions> site) =>
        {
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            var baseUrl = site.Value.BaseUrl.TrimEnd('/');
            var paths = new[] { "/", "/services", "/process", "/contact", "/privacy", "/terms" }
                .Concat(ServiceCatalog.All.Select(s => $"/services/{s.Slug}"));
            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement(ns + "urlset",
                paths.Select(p => new XElement(ns + "url", new XElement(ns + "loc", baseUrl + p)))));
            return TypedResults.Text(doc.Declaration + "\n" + doc, "application/xml", Encoding.UTF8);
        });
        return app;
    }
}
