/*
Copyright 2019 Gfi Informatique

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using BrowserDesktop.Controller;
using BrowserDesktop.Menu;
using umi3d.cdk;
using umi3d.cdk.collaboration;
using umi3d.common;
using UnityEngine;
using UnityEngine.UI;

public class ActivateDeactivateMicrophone : Singleton<ActivateDeactivateMicrophone>
{
    bool isEnvironmentLoaded = false;
    private void Start()
    {
        UMI3DEnvironmentLoader.Instance.onEnvironmentLoaded.AddListener(() => {
            isEnvironmentLoaded = true;
        });

        SessionInformationMenu.Instance.OnMicrophoneStatusChanged(!MicrophoneListener.IsMute);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(InputLayoutManager.GetInputCode(InputLayoutManager.Input.ToggleMicrophone)) && !TextInputDisplayerElement.isTyping)
        {
            ToggleMicrophoneStatus();
        }
    }

    public void ToggleMicrophoneStatus()
    {
        if (!isEnvironmentLoaded)
            return;

        MicrophoneListener.IsMute = !MicrophoneListener.IsMute;

        SessionInformationMenu.Instance.OnMicrophoneStatusChanged(!MicrophoneListener.IsMute);
    }
}
