﻿// Copyright (c) Richasy. All rights reserved.

using System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Bili.Models.App.Args;
using Bili.Models.Data.Live;
using Bili.Models.Data.Local;
using Bili.Models.Enums;
using DynamicData;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Windows.System;

namespace Bili.ViewModels.Uwp.Live
{
    /// <summary>
    /// 直播播放页面视图模型.
    /// </summary>
    public sealed partial class LivePlayerPageViewModel
    {
        private async Task OpenInBroswerAsync()
        {
            var uri = $"https://live.bilibili.com/{View.Information.Identifier.Id}";
            await Launcher.LaunchUriAsync(new Uri(uri));
        }

        private async void OnMessageReceivedAsync(object sender, LiveMessageEventArgs e)
        {
            await _dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (e.Type == LiveMessageType.ConnectSuccess)
                {
                    _heartBeatTimer.Start();
                }
                else if (e.Type == LiveMessageType.Danmaku)
                {
                    var data = e.Data as LiveDanmakuInformation;
                    Danmakus.Add(data);
                    MediaPlayerViewModel.DanmakuViewModel.AddLiveDanmakuCommand.Execute(data).Subscribe();

                    if (Danmakus.Count > 1000)
                    {
                        var removedMessages = Danmakus.Take(600).ToList();
                        Danmakus.RemoveMany(removedMessages);
                    }

                    if (IsDanmakusAutoScroll)
                    {
                        RequestDanmakusScrollToBottom?.Invoke(this, EventArgs.Empty);
                    }
                }
            });
        }

        private void Share()
        {
            var dataTransferManager = DataTransferManager.GetForCurrentView();
            dataTransferManager.DataRequested += OnShareDataRequested;
            DataTransferManager.ShowShareUI();
        }

        private void Fix()
        {
            if (_accountViewModel.State != AuthorizeState.SignedIn)
            {
                _callerViewModel.ShowTip(_resourceToolkit.GetLocaleString(LanguageNames.NeedLoginFirst), Models.Enums.App.InfoType.Warning);
                return;
            }

            if (IsLiveFixed)
            {
                _accountViewModel.RemoveFixedItemCommand.Execute(View.Information.Identifier.Id).Subscribe();
                IsLiveFixed = false;
            }
            else
            {
                _accountViewModel.AddFixedItemCommand.Execute(new FixedItem(
                    View.Information.User.Avatar.Uri,
                    View.Information.Identifier.Title,
                    View.Information.Identifier.Id,
                    Models.Enums.App.FixedType.Video)).Subscribe();
                IsLiveFixed = true;
            }
        }

        private void OnAuthorizeStateChanged(object sender, AuthorizeStateChangedEventArgs e)
            => IsSignedIn = e.NewState == Models.Enums.AuthorizeState.SignedIn;

        private void OnShareDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            var request = args.Request;
            var url = $"https://live.bilibili.com/{View.Information.Identifier.Id}";

            request.Data.Properties.Title = View.Information.Identifier.Title;
            request.Data.Properties.Description = View.Information.Description;
            request.Data.Properties.Thumbnail = RandomAccessStreamReference.CreateFromUri(View.Information.User.Avatar.GetSourceUri());
            request.Data.Properties.ContentSourceWebLink = new Uri(url);

            request.Data.SetText(View.Information.Description);
            request.Data.SetWebLink(new Uri(url));
            request.Data.SetBitmap(RandomAccessStreamReference.CreateFromUri(View.Information.User.Avatar.GetSourceUri()));
        }

        private async void OnHeartBeatTimerTickAsync(object sender, object e)
        {
            if (MediaPlayerViewModel.Status == PlayerStatus.NotLoad
                || MediaPlayerViewModel.Status == PlayerStatus.End)
            {
                return;
            }

            await _liveProvider.SendHeartBeatAsync();
        }

        private void OnDanmakusCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
            => IsDanmakusEmpty = Danmakus.Count == 0;
    }
}
