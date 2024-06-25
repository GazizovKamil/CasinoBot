using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Net.Http;
using System.Net.Http.Json;

class Program
{
    static ITelegramBotClient botClient;
    static IMongoCollection<User> userCollection;
    static HttpClient httpClient = new HttpClient();

    private static InlineKeyboardMarkup defaultKeyboard;
    private static InlineKeyboardMarkup backKeyboard;

    static async Task Main(string[] args)
    {
        string token = "7103739535:AAEQ5eOaEturOOObI9hQvhQFDbH91VMG_0A";
        botClient = new TelegramBotClient(token);

        var mongoClient = new MongoClient("mongodb://localhost:27017");
        var database = mongoClient.GetDatabase("telegram_bot_db");
        userCollection = database.GetCollection<User>("users");

        defaultKeyboard = new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithWebApp("Играть", new WebAppInfo { Url = "https://crossdata.pro/" }) },
            new [] { InlineKeyboardButton.WithCallbackData("Инфо", "info") },
            new [] { InlineKeyboardButton.WithCallbackData("Баланс", "account") },
            new [] { InlineKeyboardButton.WithCallbackData("Пополнить счет", "top_up") },
            new [] { InlineKeyboardButton.WithCallbackData("Забрать бонус", "claim_bonus") }
        });

        backKeyboard = new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("Назад", "back") }
        });

        using var cts = new CancellationTokenSource();

        ReceiverOptions receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: cts.Token
        );

        Console.WriteLine("Bot started. Press any key to exit.");
        Console.ReadKey();

        // Send cancellation request to stop bot
        cts.Cancel();
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.Message)
        {
            var message = update.Message;
            await botClient.SendTextMessageAsync(
               chatId: message.Chat.Id, "Привет",
               replyMarkup: defaultKeyboard,
               cancellationToken: cancellationToken);
        }
        else if (update.Type == UpdateType.CallbackQuery)
        {
            await HandleCallbackQueryAsync(botClient, update.CallbackQuery, cancellationToken);
        }
    }

    static async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var userId = callbackQuery.From.Id;

        switch (callbackQuery.Data)
        {
            case "info":
                await botClient.EditMessageTextAsync(
                    chatId: callbackQuery.Message.Chat.Id,
                    messageId: callbackQuery.Message.MessageId,
                    text: "Информация: Ваша задача РАЗНЫМИ СХЭМАМИ, обмануть эту ГрЕбАнУю ракетку, мы не входим в азарт, поставили, забрали!",
                    replyMarkup: backKeyboard,
                    cancellationToken: cancellationToken);
                break;

            case "account":
                var user = await userCollection.Find(Builders<User>.Filter.Eq(u => u.UserId, userId)).FirstOrDefaultAsync();
                await botClient.EditMessageTextAsync(
                    chatId: callbackQuery.Message.Chat.Id,
                    messageId: callbackQuery.Message.MessageId,
                    text: $"Ваш баланс: {user?.Balance ?? 0}",
                    replyMarkup: backKeyboard,
                    cancellationToken: cancellationToken);
                break;

            case "back":
                await botClient.EditMessageTextAsync(
                    chatId: callbackQuery.Message.Chat.Id,
                    messageId: callbackQuery.Message.MessageId,
                    text: "Привет",
                    replyMarkup: defaultKeyboard,
                    cancellationToken: cancellationToken);
                break;

            case "top_up":
                await HandleTopUpAsync(botClient, callbackQuery, cancellationToken);
                break;

            case "claim_bonus":
                await HandleClaimBonusAsync(botClient, callbackQuery, cancellationToken);
                break;

            default:
                await botClient.AnswerCallbackQueryAsync(
                    callbackQueryId: callbackQuery.Id,
                    text: "Неизвестная команда",
                    cancellationToken: cancellationToken);
                break;
        }

        // Store or update user information in MongoDB
        var filter = Builders<User>.Filter.Eq(u => u.UserId, userId);
        var updateUser = Builders<User>.Update
            .SetOnInsert(u => u.UserId, userId)
            .SetOnInsert(u => u.UserName, callbackQuery.From.Username)
            .Set(u => u.LastActivity, DateTime.UtcNow);

        await userCollection.UpdateOneAsync(filter, updateUser, new UpdateOptions { IsUpsert = true });
    }

    static async Task HandleTopUpAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        // Integrate with FK Wallet payment system
        //var userId = callbackQuery.From.Id;

        //// Assume we have a payment processing method
        //bool paymentSuccess = await ProcessPayment(userId, 100); // Process payment of 100 units

        //if (paymentSuccess)
        //{
        //    var filter = Builders<User>.Filter.Eq(u => u.UserId, userId);
        //    var update = Builders<User>.Update.Inc(u => u.Balance, 100); // Increment balance by 100

        //    await userCollection.UpdateOneAsync(filter, update);

        //    await botClient.EditMessageTextAsync(
        //        chatId: callbackQuery.Message.Chat.Id,
        //        messageId: callbackQuery.Message.MessageId,
        //        text: "Ваш баланс пополнен на 100!",
        //        replyMarkup: backKeyboard,
        //        cancellationToken: cancellationToken);
        //}
        //else
        //{
        //    await botClient.EditMessageTextAsync(
        //        chatId: callbackQuery.Message.Chat.Id,
        //        messageId: callbackQuery.Message.MessageId,
        //        text: "Ошибка при пополнении баланса.",
        //        replyMarkup: backKeyboard,
        //        cancellationToken: cancellationToken);
        //}
    }

    static async Task<bool> ProcessPayment(long userId, int amount)
    {
        //var response = await httpClient.PostAsJsonAsync("https://fk-wallet-api.com/topup", new { UserId = userId, Amount = amount });

        //return response.IsSuccessStatusCode;
        return true;
    }

    static async Task HandleClaimBonusAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var userId = callbackQuery.From.Id;
        var user = await userCollection.Find(Builders<User>.Filter.Eq(u => u.UserId, userId)).FirstOrDefaultAsync();

        if (user != null && user.LastBonusClaim != null && (DateTime.UtcNow - user.LastBonusClaim.Value).TotalHours < 24)
        {
            await botClient.AnswerCallbackQueryAsync(
                callbackQueryId: callbackQuery.Id,
                text: "Бонус можно забрать раз в 24 часа.",
                cancellationToken: cancellationToken);
        }
        else
        {
            var filter = Builders<User>.Filter.Eq(u => u.UserId, userId);
            var update = Builders<User>.Update
                .Set(u => u.LastBonusClaim, DateTime.UtcNow)
                .Inc(u => u.Balance, 50);

            await userCollection.UpdateOneAsync(filter, update);

            await botClient.EditMessageTextAsync(
                chatId: callbackQuery.Message.Chat.Id,
                messageId: callbackQuery.Message.MessageId,
                text: "Вы забрали бонус 50!",
                replyMarkup: backKeyboard,
                cancellationToken: cancellationToken);
        }
    }

    static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }
}

public class User
{
    public ObjectId Id { get; set; }
    public long UserId { get; set; }
    public string UserName { get; set; }
    public DateTime LastActivity { get; set; }
    public int Balance { get; set; }
    public DateTime? LastBonusClaim { get; set; } 
}
