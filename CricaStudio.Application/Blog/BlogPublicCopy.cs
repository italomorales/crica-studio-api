namespace CricaStudio.Application.Blog;

public static class BlogPublicCopy
{
    private static readonly Dictionary<string,(string En,string Es)> Copy = new()
    {
        ["Entre uma ideia"] = ("Between one idea", "Entre una idea"),
        ["e outra."] = ("and the next.", "y otra."),
        ["Inspiração, histórias e detalhes para criar algo que tenha a sua essência."] = ("Inspiration, stories and details to create something that feels like you.", "Inspiración, historias y detalles para crear algo con tu esencia."),
        ["Buscar matérias"] = ("Search articles", "Buscar artículos"),
        ["Buscar"] = ("Search", "Buscar"),
        ["Todas"] = ("All", "Todas"),
        ["Nenhuma matéria encontrada."] = ("No articles found.", "No se encontraron artículos."),
        ["Anterior"] = ("Previous", "Anterior"),
        ["Próxima"] = ("Next", "Siguiente"),
        ["Todas as histórias"] = ("All stories", "Todas las historias"),
        ["Inspiração e histórias da Crica Studio."] = ("Inspiration and stories from Crica Studio.", "Inspiración e historias de Crica Studio."),
        ["Empreendedorismo"] = ("Entrepreneurship", "Emprendimiento"),
        ["Renda Extra"] = ("Extra Income", "Ingresos extra"),
        ["Inspiração"] = ("Inspiration", "Inspiración"),
        ["Presentes"] = ("Gifts", "Regalos"),
        ["Processo criativo"] = ("Creative process", "Proceso creativo"),
        ["Para negócios"] = ("For businesses", "Para negocios"),
        ["Loja Crica"] = ("Crica Shop", "Tienda Crica"),
        ["Vitrine"] = ("Storefront", "Vitrina"),
        ["Ver produto"] = ("View product", "Ver producto"),
        ["A Crica ajuda pessoas a transformar ideias em presentes cheios de significado, compartilhando inspirações e dicas para criar com carinho. 💙"] = ("Crica helps people turn ideas into meaningful gifts, sharing inspiration and tips for creating with care. 💙", "Crica ayuda a las personas a transformar ideas en regalos llenos de significado, compartiendo inspiración y consejos para crear con cariño. 💙"),
    };

    public static string Text(string text,string locale) => Copy.TryGetValue(text,out var value)
        ? locale switch { "en" => value.En, "es" => value.Es, _ => text }
        : text;
}
