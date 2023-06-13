// See https://aka.ms/new-console-template for more information

// 6126013206:AAGUfS8b4E0wkAk7ECaYHWH7cC0kA2WinV4

using System.Diagnostics;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Drawing;
using System;
using static System.Net.Mime.MediaTypeNames;
using Telegram.Bot.Types.ReplyMarkups;

// prepare image data
var imageRecords = new List<HistoryRecord>();
foreach (var directory in Directory.EnumerateDirectories("C:/img"))
{
    var date = DateTime.Parse(Path.GetFileName(directory));
    var images = Directory.GetFiles(directory, "*.*").Where(s => s.EndsWith(".jpg") || s.EndsWith(".png") || s.EndsWith(".gif")).ToArray();
    var textFile = Directory.GetFiles(directory, "*.txt").First();
    var caption = Path.GetFileNameWithoutExtension(textFile);
    var text = System.IO.File.ReadAllText(textFile);

    imageRecords.Add(new HistoryRecord() { Date = date, Caption = caption, Images = images, Text = text });
}

// start bot
var botClient = new TelegramBotClient("6126013206:AAGUfS8b4E0wkAk7ECaYHWH7cC0kA2WinV4");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, _) => cts.Cancel(); // Чтобы отловить нажатие ctrl+C и всякие sigterm, sigkill, etc

var handler = new UpdateHandler(imageRecords);
var receiverOptions = new ReceiverOptions();
botClient.StartReceiving(handler, receiverOptions, cancellationToken: cts.Token);

Console.WriteLine("Bot started. Press ^C to stop");
await Task.Delay(-1, cancellationToken: cts.Token); // Такой вариант советуют MS: https://github.com/dotnet/runtime/issues/28510#issuecomment-458139641
Console.WriteLine("Bot stopped");

// Чтобы сильно не захламлять Main - это можно вынести в отдельный файл
class UpdateHandler : IUpdateHandler
{
    private IEnumerable<HistoryRecord> _historyRecords;

    private KeyboardButton _btnMoment;

    private KeyboardButton _btnRandom;

    private ReplyKeyboardMarkup _rkmMoments;

    public UpdateHandler(IEnumerable<HistoryRecord> historyRecords)
    {
        _historyRecords = historyRecords;

        _btnMoment = new KeyboardButton("Момент дня");
        _btnRandom = new KeyboardButton("Случайный момент");
        _rkmMoments = new ReplyKeyboardMarkup(new KeyboardButton[][] { new KeyboardButton[] { _btnMoment, _btnRandom } });
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        Debug.WriteLine(JsonSerializer.Serialize(update));
        // Вообще, для обработки сообщений лучше подходит паттерн "Цепочка обязанностей", но для примера тут switch-case
        // https://refactoring.guru/ru/design-patterns/chain-of-responsibility
        switch (update)
        {
            case
            {
                Type: UpdateType.Message,
                Message: { Text: { } text, Chat: { } chat },
            } when text.Equals("/start", StringComparison.OrdinalIgnoreCase):
                {
                    await botClient.SendTextMessageAsync(chat!, 
                        "Привет, единорожек :3 Этот бот позволяет вспоминть наши приятные моменты, " +
                        "подбирая ретроспективу к текущей дате запроса. В любой день просто нажимай " +
                        "кнопку \"Момент дня\" или \"Случайный момент\" - и наслаждайся <3", replyMarkup: _rkmMoments, cancellationToken: cancellationToken);
                    break;
                }
            case
            {
                Type: UpdateType.Message,
                Message: { Text: { } text, Chat: { } chat },
            } when text.Equals("Момент дня", StringComparison.OrdinalIgnoreCase):
                {
                    HistoryRecord recordToShow;

                    var dateNow = DateTime.Now.Date;
                    recordToShow = _historyRecords.FirstOrDefault(
                        record => record.Date.Day == dateNow.Day && record.Date.Month == dateNow.Month);

                    if (recordToShow is { })
                    {
                        await botClient.SendMediaGroupAsync(chat!, await PrepareMessage(recordToShow), cancellationToken: cancellationToken);
                        await botClient.SendTextMessageAsync(chat!, ":3", replyMarkup: _rkmMoments, cancellationToken: cancellationToken);
                        break;
                    }

                    var nearRecords = _historyRecords.OrderBy(record => 
                        Math.Abs((new DateTime(dateNow.Year, record.Date.Month, record.Date.Day) - dateNow).TotalDays)).Take(3);
                    var buttons = new List<List<KeyboardButton>>();
                    foreach (var record in nearRecords)
                    {
                        buttons.Add(new List<KeyboardButton>() { new KeyboardButton(record.LongCaption) });
                    }

                    var keyboard = new ReplyKeyboardMarkup(buttons);
                    await botClient.SendTextMessageAsync(chat!, "Моменты этого дня пока что не найдены, но взгляни, что нашлось неподалеку:", replyMarkup: keyboard, cancellationToken: cancellationToken);
                    break;
                }
            case
            {
                Type: UpdateType.Message,
                Message: { Text: { } text, Chat: { } chat },
            } when _historyRecords.Any(record => string.Equals(record.LongCaption, text)):
                {
                    await botClient.SendMediaGroupAsync(chat!, await PrepareMessage(_historyRecords.First(record => string.Equals(record.LongCaption, text))), cancellationToken: cancellationToken);
                    await botClient.SendTextMessageAsync(chat!, ":3", replyMarkup: _rkmMoments, cancellationToken: cancellationToken);
                    break;
                }
            case
            {
                Type: UpdateType.Message,
                Message: { Text: { } text, Chat: { } chat },
            } when text.Equals("Случайный момент", StringComparison.OrdinalIgnoreCase):
                {
                    await botClient.SendMediaGroupAsync(chat!, await PrepareMessage(_historyRecords.ElementAt(new Random().Next(_historyRecords.Count()))), cancellationToken: cancellationToken);
                    await botClient.SendTextMessageAsync(chat!, ":3", replyMarkup: _rkmMoments, cancellationToken: cancellationToken);
                    break;
                }
            case
            {
                Type: UpdateType.Message,
                Message.Chat: { } chat
            }:
                {
                    await botClient.SendTextMessageAsync(chat!, "Что-то пошло не так... попробуй нажать кнопку еще раз?", replyMarkup: _rkmMoments, cancellationToken: cancellationToken);
                    break;
                }
        }
    }

    async Task<IEnumerable<InputMediaPhoto>> PrepareMessage(HistoryRecord recordToShow)
    {
        List<InputMediaPhoto> media = new List<InputMediaPhoto>();
        foreach (var image in recordToShow.Images)
        {
            media.Add(new InputMediaPhoto(new InputMedia(await ImageAdd(image), Path.GetFileName(image))));
        }

        var tempMedia = media.FirstOrDefault();
        if (tempMedia != null)
            tempMedia.Caption = recordToShow.Message;

        return media;
    }

    async Task<Stream> ImageAdd(string imagePath)
    {
        MemoryStream memStream = new MemoryStream();
        await memStream.WriteAsync(System.IO.File.ReadAllBytes(imagePath));
        memStream.Seek(0, SeekOrigin.Begin);
        return memStream;
    }
    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.Error.WriteLine(exception);
        return Task.CompletedTask;
    }

    public Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.Error.WriteLine(exception);
        return Task.CompletedTask;
    }
}

internal class HistoryRecord
{
    public string[] Images { get; set; }

    public string Caption { get; set; }

    public string LongCaption => $"{Date.ToShortDateString()} - {Caption}";

    public string Text { get; set; }

    public DateTime Date { get; set; }

    public string Message => $"{LongCaption}{Environment.NewLine}{Environment.NewLine}{Text}";

}