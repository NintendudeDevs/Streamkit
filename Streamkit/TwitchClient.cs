﻿using System;
using System.Timers;
using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;

using Streamkit.Core;

namespace Streamkit.Twitch {
    public class TwitchBot {
        public static TwitchBot Instance;

        private TwitchClient client;
        private Timer timer;

        public TwitchBot() {
            createClient();

            timer = new Timer(1000 * 60 * 60);
            timer.Elapsed += (sender, args) => {
                try {
                    Logger.Log("Restarting twitch client...");
                    this.client.Disconnect();
                }
                catch {
                    Logger.Log("Failed to disconnect.");
                }

                createClient();

            };
            timer.Start();
        }

        public void JoinChannel(string channelName) {
            this.client.JoinChannel(channelName);
        }

        private void createClient() {
            ConnectionCredentials credentials = new ConnectionCredentials(
                   "BotZura", Config.TwitchChatToken);

            client = new TwitchClient();
            client.Initialize(credentials);

            client.OnConnected += onConnected;
            client.OnJoinedChannel += onJoinedChannel;
            client.OnMessageReceived += onMessageReceived;
            client.OnNewSubscriber += onNewSubscriber;
            client.OnReSubscriber += onResubsriber;
            client.OnGiftedSubscription += onGiftedSubscription;

            Instance = this;

            client.Connect();
        }

        private void onConnected(object sender, OnConnectedArgs e) {
            Logger.Log("Twitch client connected.");
            foreach (string username in UserManager.GetTwitchUsernames()) {
                this.JoinChannel(username);
            }
        }

        private void onJoinedChannel(object sender, OnJoinedChannelArgs e) {
            try {
                Logger.Log("Joined channel " + e.Channel);

            } catch(Exception ex) {
                Logger.Log(ex);
            }
        }

        private void onMessageReceived(object sender, OnMessageReceivedArgs e) {
            try {
                if (e.ChatMessage.Bits > 0) {
                    Logger.Log(e.ChatMessage.Bits + " bits cheered in " + e.ChatMessage.Channel);
                    User user = UserManager.GetUserTwitch(e.ChatMessage.Channel);
                    BitbarManager.AddBits(user, e.ChatMessage.Bits);
                }
            }
            catch (Exception ex) {
                Logger.Log(ex);
            }
        }

        private void onNewSubscriber(object sender, OnNewSubscriberArgs e) {
            try {
                Logger.Log("New subscriber in " + e.Channel);

                User user = UserManager.GetUserTwitch(e.Channel);
                BitbarManager.AddBits(user, subPlanToBits(e.Subscriber.SubscriptionPlan));
            }
            catch (Exception ex) {
                Logger.Log(ex);
            }
        }

        private void onResubsriber(object sender, OnReSubscriberArgs e) {
            try {
                Logger.Log("Resubscriber in " + e.Channel);

                User user = UserManager.GetUserTwitch(e.Channel);
                BitbarManager.AddBits(user, subPlanToBits(e.ReSubscriber.SubscriptionPlan));
            }
            catch (Exception ex) {
                Logger.Log(ex);
            }
        }

        private void onGiftedSubscription(object sender, OnGiftedSubscriptionArgs e) {
            try {
                Logger.Log("Gifted subscription in " + e.Channel);

                // TODO: Find out if this event is fired by itself or alongside another.

                //User user = UserManager.GetUserTwitch(e.Channel);
                //// It seems you can't gift sub tiers higher than one yet
                //BitbarManager.AddBits(user, 250);
            }
            catch (Exception ex) {
                Logger.Log(ex);
            }
        }

        private static int subPlanToBits(SubscriptionPlan plan) {
            if (plan == SubscriptionPlan.Tier3) return 1250;
            if (plan == SubscriptionPlan.Tier2) return 500;
            return 250;
        }
    }
}