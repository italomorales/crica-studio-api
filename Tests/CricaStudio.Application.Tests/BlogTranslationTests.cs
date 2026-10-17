using System.Text.Json;
using CricaStudio.Application.Blog;
using CricaStudio.Domain.Blog;
using Xunit;

namespace CricaStudio.Application.Tests;

public sealed class BlogTranslationTests
{
    [Theory]
    [InlineData("en", "View product", "Crica Shop")]
    [InlineData("es", "Ver producto", "Tienda Crica")]
    [InlineData("pt", "Ver produto", "Loja Crica")]
    public void Embedded_product_actions_use_article_language_without_rewriting_editorial_content(string locale,string action,string label)
    {
        var id=Guid.NewGuid();
        var document=JsonSerializer.SerializeToElement(new { type="doc",content=new[] {
            new { type="blogProduct",attrs=new { id=id.ToString(),kind="shop" } }
        } });
        var product=new BlogProduct(id,"shop","Nome editorial","Descrição editorial","/assets/mug.webp","/loja/caneca","Loja Crica");
        var html=BlogDocument.Render(document,[product],locale);
        Assert.Contains(action,html);
        Assert.Contains(label,html);
        Assert.Contains("Nome editorial",html);
        Assert.Contains("href=\"/loja/caneca\"",html);
    }

    [Theory]
    [InlineData("en","All stories","Entrepreneurship")]
    [InlineData("es","Todas las historias","Emprendimiento")]
    [InlineData("pt","Todas as histórias","Empreendedorismo")]
    public void Server_rendered_labels_match_the_selected_locale(string locale,string back,string category)
    {
        Assert.Equal(back,BlogPublicCopy.Text("Todas as histórias",locale));
        Assert.Equal(category,BlogPublicCopy.Text("Empreendedorismo",locale));
        Assert.Equal("Categoria personalizada",BlogPublicCopy.Text("Categoria personalizada",locale));
    }
}
