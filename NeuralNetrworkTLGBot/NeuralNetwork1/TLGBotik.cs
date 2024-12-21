using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;


namespace NeuralNetwork1
{
    class TLGBotik
    {
        public Telegram.Bot.TelegramBotClient botik = null;

        private UpdateTLGMessages formUpdater;

        public PictureBox pb = new PictureBox();
        public GenerateImage generator = new GenerateImage();
        private BaseNetwork perseptron = null;
        private AIMLBotik AIMLbot = null;
        // CancellationToken - инструмент для отмены задач, запущенных в отдельном потоке
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        public TLGBotik(BaseNetwork net, UpdateTLGMessages updater, PictureBox pb, GenerateImage gi)
        {
            var botKey = System.IO.File.ReadAllText("botkey.txt");
            botik = new Telegram.Bot.TelegramBotClient(botKey);
            formUpdater = updater;
            perseptron = net;
            AIMLbot = new AIMLBotik();
            this.pb = pb;
            generator = gi;
        }

        public void SetNet(BaseNetwork net)
        {
            perseptron = net;
            formUpdater("Net updated!");
        }

        private async Task HandleUpdateMessageAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            //  Тут очень простое дело - банально отправляем назад сообщения
            var message = update.Message;
            formUpdater("Тип сообщения : " + message.Type.ToString());

            //  Получение файла (картинки)
            if (message.Type == Telegram.Bot.Types.Enums.MessageType.Photo)
            {
                formUpdater("Picture loadining started");
                var photoId = message.Photo.Last().FileId;
                Telegram.Bot.Types.File fl = botik.GetFileAsync(photoId).Result;
                var imageStream = new MemoryStream();
                await botik.DownloadFileAsync(fl.FilePath, imageStream, cancellationToken: cancellationToken);
                var img = System.Drawing.Image.FromStream(imageStream);

                System.Drawing.Bitmap bm = new System.Drawing.Bitmap(img, 100,100);
                

                //  Масштабируем aforge
                AForge.Imaging.Filters.ResizeBilinear scaleFilter = new AForge.Imaging.Filters.ResizeBilinear(100, 100);
                var uProcessed = scaleFilter.Apply(AForge.Imaging.UnmanagedImage.FromManagedImage(bm));

                Sample sample = generator.GenerateExtFigure(bm, this.pb);
                pb.Image = bm;

                switch (perseptron.Predict(sample))
                {
                    case FigureType.Mercury: botik.SendTextMessageAsync(message.Chat.Id, "Это легко, это Меркурий!"); break;
                    case FigureType.Venus: botik.SendTextMessageAsync(message.Chat.Id, "Это легко, Венера!"); break;
                    case FigureType.Earth: botik.SendTextMessageAsync(message.Chat.Id, "Земля!"); break;
                    case FigureType.Mars: botik.SendTextMessageAsync(message.Chat.Id, "Это легко, это был Марс!"); break;
                    case FigureType.Jupiter: botik.SendTextMessageAsync(message.Chat.Id, "Это Юпитер!"); break;
                    case FigureType.Saturn: botik.SendTextMessageAsync(message.Chat.Id, "Это Сатурн!"); break;
                    case FigureType.Uranus: botik.SendTextMessageAsync(message.Chat.Id, "Это явно Уран!"); break;
                    case FigureType.Neptune: botik.SendTextMessageAsync(message.Chat.Id, "Однозначно Нептун!"); break;
                    case FigureType.Sun: botik.SendTextMessageAsync(message.Chat.Id, "Это просто, Солнце!"); break;
                    case FigureType.Moon: botik.SendTextMessageAsync(message.Chat.Id, "Я уверен, что это Луна!"); break;
                    default: botik.SendTextMessageAsync(message.Chat.Id, "Я такого не знаю!"); break;
                }

                formUpdater("Picture recognized!");
                return;
            }

            if (message.Type == MessageType.Text)
            {
                string userMessage = message.Text;

                // Используем AIML-бота для ответа
                string aimlResponse = AIMLbot.Talk(userMessage);

                if (!string.IsNullOrEmpty(aimlResponse))
                {
                    await botik.SendTextMessageAsync(message.Chat.Id, aimlResponse, cancellationToken: cancellationToken);
                }
                else
                {
                    await botik.SendTextMessageAsync(message.Chat.Id, "Я не понял, что вы имели в виду.", cancellationToken: cancellationToken);
                }

                formUpdater(userMessage);
                return;
            }

            if (message == null || message.Type != MessageType.Text) return;

            //botik.SendTextMessageAsync(message.Chat.Id, "Bot reply : " + message.Text);
            formUpdater(message.Text);
            return;
        }
        Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var apiRequestException = exception as ApiRequestException;
            if (apiRequestException != null)
                Console.WriteLine($"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}");
            else
                Console.WriteLine(exception.ToString());
            return Task.CompletedTask;
        }

        public bool Act()
        {
            try
            {
                botik.StartReceiving(HandleUpdateMessageAsync, HandleErrorAsync, new ReceiverOptions
                {   // Подписываемся только на сообщения
                    AllowedUpdates = new[] { UpdateType.Message }
                },
                cancellationToken: cts.Token);
                // Пробуем получить логин бота - тестируем соединение и токен
                Console.WriteLine($"Connected as {botik.GetMeAsync().Result}");
            }
            catch (Exception e)
            {
                return false;
            }
            return true;
        }

        public void Stop()
        {
            cts.Cancel();
        }

    }
}
