# voice.py
#
# Handles ElevenLabs TTS generation.

import requests

class Voice:
    def __init__(self, elevenlabs_api_key: str, voice_id: str = "Ib4kDyWcM5DppIOQH52e"):
        self.elevenlabs_api_key = elevenlabs_api_key
        self.voice_id = voice_id

    def generate_tts(self, text_to_speak: str):
        """
        Send text to ElevenLabs API, return raw audio bytes (mp3).
        """
        url = f"https://api.elevenlabs.io/v1/text-to-speech/{self.voice_id}"
        headers = {
            "xi-api-key": self.elevenlabs_api_key,
            "Content-Type": "application/json"
        }
        payload = {
            "text": text_to_speak,
            "voice_settings": {
                "stability": 0.3,
                "similarity_boost": 0.75
            }
        }
        response = requests.post(url, json=payload, headers=headers)
        if response.status_code == 200:
            return response.content  # mp3 data
        else:
            print("Error in TTS generation:", response.status_code, response.text)
            return None
