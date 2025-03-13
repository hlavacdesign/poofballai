# server.py
#
# A Flask server that:
#   1) Takes user input from a POST /chat endpoint
#   2) Uses Pinecone for embedding + query (with an existing index/memory)
#   3) Calls a chat-based LLM (via ChatOpenAI from langchain_community)
#   4) Returns structured JSON with keys: long_response, short_response, audio_url, media_urls
#   5) Uses ElevenLabs to generate TTS from short_response
#   6) Serves MP3 files at /audio/<filename>
#
# The logic is now split into multiple modules: memory.py, agent.py, voice.py

import os
import uuid
import json
import flask
from flask import Flask, request, jsonify, send_from_directory
from flask_cors import CORS
from dotenv import load_dotenv

# Import the refactored classes
from memory import Memory
from agent import Agent
from voice import Voice

# ------------------------------
# Load environment variables
# ------------------------------
load_dotenv()

OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")
PINECONE_API_KEY = os.getenv("PINECONE_API_KEY")
ELEVENLABS_API_KEY = os.getenv("ELEVENLABS_API_KEY")

# ------------------------------
# Flask setup
# ------------------------------
app = Flask(__name__)
CORS(app)  # Allow all origins or specify in production if needed

# ------------------------------
# Instantiate helper classes
# ------------------------------
INDEX_NAME = "versionone"
NAMESPACE = "ns1"

memory = Memory(PINECONE_API_KEY, INDEX_NAME, NAMESPACE)
agent = Agent(openai_api_key=OPENAI_API_KEY, model_name="gpt-4o-mini", temperature=0.7)
voice = Voice(elevenlabs_api_key=ELEVENLABS_API_KEY, voice_id="Ib4kDyWcM5DppIOQH52e")

# --------------------------------------------------
# 1) Flask route: /chat
# --------------------------------------------------
@app.route("/chat", methods=["POST"])
def chat():
    data = request.get_json(force=True)
    user_message = data.get("message", "").strip()
    if not user_message:
        return jsonify({
            "long_response": "No message received.",
            "short_response": "",
            "audio_url": "",
            "media_urls": []
        })

    # 1a) Retrieve Pinecone context
    results = memory.retrieve_context(user_message)
    if results is None:
        return jsonify({
            "long_response": "Error retrieving context from Pinecone.",
            "short_response": "",
            "audio_url": "",
            "media_urls": []
        })

    # Gather relevant text & URLs from matches
    context_str = ""
    matches = results.matches or []
    if not matches:
        print("[DEBUG] No matches returned from Pinecone!")
    else:
        print(f"[DEBUG] Pinecone returned {len(matches)} matches.")

    for m in matches:
        md = m.metadata
        print(f"[DEBUG] Match ID: {m.id}, Score: {m.score}")
        # Append text
        if "text" in md:
            snippet = md["text"][:80].replace("\n", " ")
            print(f"[DEBUG] Appending text from metadata: '{snippet}...'")
            context_str += md["text"] + "\n\n"
        # If there are any URLs in the metadata, add them to the context
        if "urls" in md:
            url_list_str = "\n".join(md["urls"])
            print(f"[DEBUG] Found {len(md['urls'])} URLs in metadata: {md['urls']}")
            context_str += f"Possible relevant URLs:\n{url_list_str}\n\n"

    # 1b) Send to the LLM (Agent)
    raw_llm_output = agent.run(user_message, context_str)
    if not raw_llm_output:
        return jsonify({
            "long_response": "Sorry, encountered an error generating the answer.",
            "short_response": "",
            "audio_url": "",
            "media_urls": []
        })

    print("[DEBUG] LLM raw output:")
    print(raw_llm_output)

    # 1c) Parse the LLM JSON
    long_answer = ""
    short_answer = ""
    media_urls = []

    try:
        parsed = json.loads(raw_llm_output)
        long_answer = parsed.get("long_answer", "").strip()
        short_answer = parsed.get("short_answer", "").strip()
        media_urls = parsed.get("media_urls", [])
        if not isinstance(media_urls, list):
            media_urls = []
    except Exception:
        print("LLM output not valid JSON. Fallback to entire text as long_answer.")
        long_answer = raw_llm_output
        short_answer = "Here is a short summary."
        media_urls = []

    if media_urls:
        print(f"[DEBUG] LLM returned these media URLs: {media_urls}")

    # 1d) Generate TTS from short_answer
    audio_data = voice.generate_tts(short_answer)
    if not audio_data:
        audio_url = ""
    else:
        filename = f"audio_{uuid.uuid4().hex}.mp3"
        filepath = os.path.join("audio_files", filename)
        os.makedirs("audio_files", exist_ok=True)
        with open(filepath, "wb") as f:
            f.write(audio_data)
        audio_url = request.host_url + "audio/" + filename

    # 1e) Return combined JSON
    return jsonify({
        "long_response": long_answer,
        "short_response": short_answer,
        "audio_url": audio_url,
        "media_urls": media_urls
    })

# --------------------------------------------------
# 2) Serve audio files
# --------------------------------------------------
@app.route("/audio/<path:filename>", methods=["GET"])
def serve_audio(filename):
    return send_from_directory("audio_files", filename)

# --------------------------------------------------
# 3) Run the Flask server
# --------------------------------------------------
if __name__ == "__main__":
    # For debugging purposes, set debug=True.
    app.run(host="0.0.0.0", port=5000, debug=True)
