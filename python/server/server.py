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
# Now includes logic to forward any retrieved Pinecone URLs to the LLM so that,
# if relevant, the LLM can return them in "media_urls". Also prints out debug info
# if the LLM does indeed return media URLs in its final JSON.

import os
import uuid
import flask
from flask import Flask, request, jsonify, send_from_directory
import requests
import json

# Pinecone new client interface:
from pinecone import Pinecone

# For the chat-based LLM from langchain_community:
from langchain_community.chat_models import ChatOpenAI
from langchain.schema import HumanMessage

# ------------------------------
# SETUP API KEYS
# ------------------------------
import os
from dotenv import load_dotenv

# Load .env variables if they exist
load_dotenv()

OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")
PINECONE_API_KEY = os.getenv("PINECONE_API_KEY")
ELEVENLABS_API_KEY = os.getenv("ELEVENLABS_API_KEY")

# print(f"OPENAI_API_KEY: {OPENAI_API_KEY}")
# print(f"PINECONE_API_KEY: {PINECONE_API_KEY}")
# print(f"ELEVENLABS_API_KEY: {ELEVENLABS_API_KEY}")

# ------------------------------
# CORS IMPORT AND SETUP (ADDED)
# ------------------------------
from flask_cors import CORS

app = Flask(__name__)

# You can limit allowed origins if needed:
# CORS(app, resources={r"/*": {"origins": ["https://hlavac.ai"]}})
# For now, this will allow all origins:
CORS(app)

# --------------------------------------------------
# 1) Pinecone Setup
# --------------------------------------------------
INDEX_NAME = "versionone"
NAMESPACE = "ns1"

pc = Pinecone(PINECONE_API_KEY)
index = pc.Index(INDEX_NAME)

# --------------------------------------------------
# 2) ChatOpenAI LLM
# --------------------------------------------------
llm = ChatOpenAI(
    openai_api_key=OPENAI_API_KEY,
    model_name="gpt-4o-mini",
    temperature=0.7
)

# --------------------------------------------------
# 3) ElevenLabs config
# --------------------------------------------------
ELEVENLABS_VOICE_ID = "Ib4kDyWcM5DppIOQH52e"

def generate_elevenlabs_tts(text_to_speak):
    """Send text to ElevenLabs API, return raw audio bytes (mp3)."""
    url = f"https://api.elevenlabs.io/v1/text-to-speech/{ELEVENLABS_VOICE_ID}"
    headers = {
        "xi-api-key": ELEVENLABS_API_KEY,
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

# --------------------------------------------------
# 4) Flask route: /chat
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

    # 4a) Use Pinecone to retrieve context
    try:
        # Embed user query
        x = pc.inference.embed(
            model="llama-text-embed-v2",
            inputs=[user_message],
            parameters={"input_type": "query"}
        )
        vec_length = len(x[0].values)
        print(f"[DEBUG] Embedded query vector length: {vec_length}")
        if vec_length > 5:
            print(f"[DEBUG] First 5 vector elements: {x[0].values[:5]}")

        # Query
        results = index.query(
            namespace=NAMESPACE,
            vector=x[0].values,
            top_k=3,
            include_values=False,
            include_metadata=True
        )

        # Convert QueryResponse to a dict for debug
        debug_dict = {
            "matches": [],
            "namespace": results.namespace
        }
        if results.matches is not None:
            for match in results.matches:
                debug_dict["matches"].append({
                    "id": match.id,
                    "score": match.score,
                    "metadata": match.metadata
                })
        print("[DEBUG] Pinecone query results (raw, as dict):")
        print(json.dumps(debug_dict, indent=2))

    except Exception as e:
        print("Pinecone retrieval error:", e)
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
        # If there are any URLs in the metadata, also add them to the context
        if "urls" in md:
            # Turn the list of URLs into a single string to pass along
            url_list_str = "\n".join(md["urls"])
            print(f"[DEBUG] Found {len(md['urls'])} URLs in metadata: {md['urls']}")
            context_str += f"Possible relevant URLs:\n{url_list_str}\n\n"

    # 4b) Create a single string prompt to pass as a HumanMessage
    prompt = f"""
You are Version One, a virtual representation of Michal Hlavac. 
You are talking to the user as Michal may speak in first person on Michal's behalf.

User question:
{user_message}

Relevant context:
{context_str}

Generate a STRICT JSON object with the keys "long_answer", "short_answer", and "media_urls". Example:
{{
  "long_answer": "...",
  "short_answer": "...",
  "media_urls": ["...", "..."]
}}

Where:
- long_answer is a very short response to the user, using the context if relevant
- short_answer is a concise summary
- media_urls is a list of one or more URLs IF relevant, otherwise an empty list.

Note: If any URLs in the context are relevant, please include them in "media_urls". 
Output ONLY valid JSON, with no extra commentary.
"""

    messages = [HumanMessage(content=prompt)]

    # 4c) Call the LLM
    try:
        llm_response = llm.predict_messages(messages)
        raw_llm_output = llm_response.content
    except Exception as e:
        print("Error calling LLM:", e)
        return jsonify({
            "long_response": "Sorry, encountered an error generating the answer.",
            "short_response": "",
            "audio_url": "",
            "media_urls": []
        })

    # Print out what the LLM returned for debug
    print("[DEBUG] LLM raw output:")
    print(raw_llm_output)

    # 4d) Parse the LLM JSON
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
    except Exception as ex:
        print("LLM output not valid JSON. Fallback to entire text as long_answer.")
        long_answer = raw_llm_output
        short_answer = "Here is a short summary."
        media_urls = []

    if media_urls:
        print(f"[DEBUG] LLM returned these media URLs: {media_urls}")

    # 4e) Generate TTS from short_answer
    audio_data = generate_elevenlabs_tts(short_answer)
    if not audio_data:
        audio_url = ""
    else:
        filename = f"audio_{uuid.uuid4().hex}.mp3"
        filepath = os.path.join("audio_files", filename)
        os.makedirs("audio_files", exist_ok=True)
        with open(filepath, "wb") as f:
            f.write(audio_data)
        audio_url = request.host_url + "audio/" + filename

    # 4f) Return combined JSON
    return jsonify({
        "long_response": long_answer,
        "short_response": short_answer,
        "audio_url": audio_url,
        "media_urls": media_urls
    })

# --------------------------------------------------
# 5) Serve audio files
# --------------------------------------------------
@app.route("/audio/<path:filename>", methods=["GET"])
def serve_audio(filename):
    return send_from_directory("audio_files", filename)

# --------------------------------------------------
# 6) Run the Flask server
# --------------------------------------------------
if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000, debug=True)
