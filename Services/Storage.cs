﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.IoTSolutions.UIConfig.Services.Exceptions;
using Microsoft.Azure.IoTSolutions.UIConfig.Services.External;
using Microsoft.Azure.IoTSolutions.UIConfig.Services.Models;
using Microsoft.Azure.IoTSolutions.UIConfig.Services.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.IoTSolutions.UIConfig.Services
{
    public interface IStorage
    {
        Task<object> GetThemeAsync();
        Task<object> SetThemeAsync(object theme);
        Task<object> GetUserSetting(string id);
        Task<object> SetUserSetting(string id, object setting);
        Task<Logo> GetLogoAsync();
        Task<Logo> SetLogoAsync(Logo model);
        Task<IEnumerable<DeviceGroup>> GetAllDeviceGroupsAsync();
        Task<DeviceGroup> GetDeviceGroupAsync(string id);
        Task<DeviceGroup> CreateDeviceGroupAsync(DeviceGroup input);
        Task<DeviceGroup> UpdateDeviceGroupAsync(string id, DeviceGroup input, string etag);
        Task DeleteDeviceGroupAsync(string id);
        Task<IEnumerable<Profile>> GetAllProfilesAsync();
        Task<Profile> GetProfileAsync(string id);
        Task<Profile> CreateProfileAsync(Profile input);
        Task<Profile> UpdateProfileAsync(string id, Profile input, string etag);
        Task DeleteProfileAsync(string id);
    }

    public class Storage : IStorage
    {
        private readonly IStorageAdapterClient client;
        private readonly IServicesConfig config;

        internal const string SOLUTION_COLLECTION_ID = "solution-settings";
        internal const string THEME_KEY = "theme";
        internal const string LOGO_KEY = "logo";
        internal const string USER_COLLECTION_ID = "user-settings";
        internal const string DEVICE_GROUP_COLLECTION_ID = "devicegroups";
        internal const string PROFILE_COLLECTION_ID = "profiles";
        private const string BING_MAP_KEY_KEY = "BingMapKey";

        public Storage(
            IStorageAdapterClient client,
            IServicesConfig config)
        {
            this.client = client;
            this.config = config;
        }

        public async Task<object> GetThemeAsync()
        {
            string data;

            try
            {
                var response = await this.client.GetAsync(SOLUTION_COLLECTION_ID, THEME_KEY);
                data = response.Data;
            }
            catch (ResourceNotFoundException)
            {
                data = JsonConvert.SerializeObject(Theme.Default);
            }

            var themeOut = JsonConvert.DeserializeObject(data) as JToken ?? new JObject();
            this.AppendBingMapKey(themeOut);
            return themeOut;
        }

        public async Task<object> SetThemeAsync(object themeIn)
        {
            var value = JsonConvert.SerializeObject(themeIn);
            var response = await this.client.UpdateAsync(SOLUTION_COLLECTION_ID, THEME_KEY, value, "*");
            var themeOut = JsonConvert.DeserializeObject(response.Data) as JToken ?? new JObject();
            this.AppendBingMapKey(themeOut);
            return themeOut;
        }

        private void AppendBingMapKey(JToken theme)
        {
            if (theme[BING_MAP_KEY_KEY] == null)
            {
                theme[BING_MAP_KEY_KEY] = this.config.BingMapKey;
            }
        }

        public async Task<object> GetUserSetting(string id)
        {
            try
            {
                var response = await this.client.GetAsync(USER_COLLECTION_ID, id);
                return JsonConvert.DeserializeObject(response.Data);
            }
            catch (ResourceNotFoundException)
            {
                return new object();
            }
        }

        public async Task<object> SetUserSetting(string id, object setting)
        {
            var value = JsonConvert.SerializeObject(setting);
            var response = await this.client.UpdateAsync(USER_COLLECTION_ID, id, value, "*");
            return JsonConvert.DeserializeObject(response.Data);
        }

        public async Task<Logo> GetLogoAsync()
        {
            try
            {
                var response = await this.client.GetAsync(SOLUTION_COLLECTION_ID, LOGO_KEY);
                return JsonConvert.DeserializeObject<Logo>(response.Data);
            }
            catch (ResourceNotFoundException)
            {
                return Logo.Default;
            }
        }

        public async Task<Logo> SetLogoAsync(Logo model)
        {
            //Do not overwrite existing name or image with null
            if(model.Name == null || model.Image == null)
            {
                Logo current = await this.GetLogoAsync();
                if(!current.IsDefault)
                {
                    model.Name = model.Name ?? current.Name;
                    if (model.Image == null && current.Image != null)
                    {
                        model.Image = current.Image;
                        model.Type = current.Type;
                    }
                }
            }

            var value = JsonConvert.SerializeObject(model);
            var response = await this.client.UpdateAsync(SOLUTION_COLLECTION_ID, LOGO_KEY, value, "*");
            return JsonConvert.DeserializeObject<Logo>(response.Data);
        }

        public async Task<IEnumerable<DeviceGroup>> GetAllDeviceGroupsAsync()
        {
            var response = await this.client.GetAllAsync(DEVICE_GROUP_COLLECTION_ID);
            return response.Items.Select(this.CreateGroupServiceModel);
        }

        public async Task<DeviceGroup> GetDeviceGroupAsync(string id)
        {
            var response = await this.client.GetAsync(DEVICE_GROUP_COLLECTION_ID, id);
            return this.CreateGroupServiceModel(response);
        }

        public async Task<DeviceGroup> CreateDeviceGroupAsync(DeviceGroup input)
        {
            var value = JsonConvert.SerializeObject(input, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var response = await this.client.CreateAsync(DEVICE_GROUP_COLLECTION_ID, value);
            return this.CreateGroupServiceModel(response);
        }

        public async Task<DeviceGroup> UpdateDeviceGroupAsync(string id, DeviceGroup input, string etag)
        {
            var value = JsonConvert.SerializeObject(input, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var response = await this.client.UpdateAsync(DEVICE_GROUP_COLLECTION_ID, id, value, etag);
            return this.CreateGroupServiceModel(response);
        }

        public async Task DeleteDeviceGroupAsync(string id)
        {
            await this.client.DeleteAsync(DEVICE_GROUP_COLLECTION_ID, id);
        }

        private DeviceGroup CreateGroupServiceModel(ValueApiModel input)
        {
            var output = JsonConvert.DeserializeObject<DeviceGroup>(input.Data);
            output.Id = input.Key;
            output.ETag = input.ETag;
            return output;
        }

        public async Task<IEnumerable<Profile>> GetAllProfilesAsync()
        {
            var response = await this.client.GetAllAsync(PROFILE_COLLECTION_ID);
            return response.Items.Select(this.CreateProfileServiceModel);
        }

        public async Task<Profile> GetProfileAsync(string id)
        {
            var response = await this.client.GetAsync(PROFILE_COLLECTION_ID, id);
            return this.CreateProfileServiceModel(response);
        }

        public async Task<Profile> CreateProfileAsync(Profile input)
        {
            var value = JsonConvert.SerializeObject(input, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var response = await this.client.CreateAsync(PROFILE_COLLECTION_ID, value);
            return this.CreateProfileServiceModel(response);
        }

        public async Task<Profile> UpdateProfileAsync(string id, Profile input, string etag)
        {
            var value = JsonConvert.SerializeObject(input, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var response = await this.client.UpdateAsync(PROFILE_COLLECTION_ID, id, value, etag);
            return this.CreateProfileServiceModel(response);
        }

        public async Task DeleteProfileAsync(string id)
        {
            await this.client.DeleteAsync(PROFILE_COLLECTION_ID, id);
        }

        private Profile CreateProfileServiceModel(ValueApiModel input)
        {
            var output = JsonConvert.DeserializeObject<Profile>(input.Data);
            output.Id = input.Key;
            output.ETag = input.ETag;
            return output;
        }
    }
}
