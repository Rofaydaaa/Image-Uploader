using Microsoft.Extensions.FileProviders;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseStaticFiles();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "uploads")),
    RequestPath = "/uploads"
});

app.MapGet("/", async () =>
{
    string html = await File.ReadAllTextAsync("./index.html");
    return Results.Content(html, "text/html");
});

app.MapPost("/", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    var title = form["imageTitle"];
    var imgFile = form.Files.GetFile("imageFile");
    
    if (form.Keys.Count == 0 || string.IsNullOrEmpty(title))
    {
        return Results.BadRequest(new { error = "Empty title string"});
    }
    
    if (form.Files.Count == 0 || imgFile is null)
    {
        return Results.BadRequest(new { error = "No file uploaded"});
    }

    string imgName = Path.GetFileName(imgFile.FileName);
    string extension = Path.GetExtension(imgName).ToLower();
    if (extension != ".png" && extension != ".jpg" && extension != ".jpeg" && extension != ".gif")
    {
        return Results.BadRequest(new { error = "Invalid uploaded format"});
    }

    string imageID =  Guid.NewGuid().ToString();
    string targetDirectory = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
    string targetFilePath = Path.Combine(targetDirectory, imageID + extension);
    
    if (!Directory.Exists(targetDirectory))
    {
        Directory.CreateDirectory(targetDirectory);
    }
    
    using (var stream = new FileStream(targetFilePath, FileMode.Create))
    {
        await imgFile.CopyToAsync(stream);
    }

    string jsonFile = Path.Combine(Directory.GetCurrentDirectory(), "data.json");
    
    var imageDetails = new ImageDetails
    {
        ID = imageID,
        Title = title.ToString(),
        Path = $"/uploads/{imageID}{extension}"
    };

    var options = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    var imageList = new List<ImageDetails>();;
    bool fileExists = File.Exists(jsonFile);
    if (fileExists)
    {
        string json = await File.ReadAllTextAsync(jsonFile);
        imageList = JsonSerializer.Deserialize<List<ImageDetails>>(json);
    }

    imageList.Add(imageDetails);
    string updatedJson = JsonSerializer.Serialize(imageList, options);

    await File.WriteAllTextAsync(jsonFile, updatedJson);

    return Results.Redirect($"/picture/{imageID}");
});

app.MapGet("/picture/{id}", async (string id) =>
{
    string jsonFile = Path.Combine(Directory.GetCurrentDirectory(), "data.json");
    string json = await File.ReadAllTextAsync(jsonFile);
    List<ImageDetails>? imageList = JsonSerializer.Deserialize<List<ImageDetails>>(json);
    var image = imageList.FirstOrDefault(i => i.ID == id);

    if (image != null)
    {
        var html = $@"
            <html>
            <head>
                <style>
                    body {{
                        font-family: Arial, sans-serif;
                        background-color: #f2f2f2;
                        display: flex;
                        justify-content: center;
                        align-items: center;
                        height: 100vh;
                        margin: 0;
                        padding: 20px;
                    }}

                    .content {{
                        text-align: center;
                    }}

                    h2 {{
                        color: #333;
                        margin-bottom: 20px;
                    }}

                    img {{
                        max-width: 500px;
                        height: auto;
                        margin-bottom: 20px;
                    }}

                    .goback-btn {{
                        padding: 10px 20px;
                        background-color: #333;
                        color: #fff;
                        text-decoration: none;
                        border: none;
                        border-radius: 4px;
                        cursor: pointer;
                    }}

                    .goback-btn:hover {{
                        background-color: #555;
                    }}
                </style>
            </head>
            <body>
                <div class=""content"">
                    <h2>Title: {image.Title}</h2>
                    <img src=""{image.Path}"" alt=""{image.Title}"" />
                    <br/><br/>
                    <button class=""goback-btn"" onclick=""window.location.href='/';"">Go Back</button>
                </div>
            </body>
            </html>
            ";
        return Results.Content(html, "text/html");
    }
    else
    {
        return Results.StatusCode(404);
    }
});

app.Run();