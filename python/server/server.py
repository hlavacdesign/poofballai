import os
from flask import Flask, request, jsonify
from flask_cors import CORS
from openai import OpenAI

# Make sure you have OPENAI_API_KEY set in your environment
api_key = os.getenv("OPENAI_API_KEY", "YOUR-API-KEY")
client = OpenAI(api_key=api_key)

app = Flask(__name__)
CORS(app)

@app.route('/chat', methods=['POST'])
def chat():
    data = request.get_json()
    user_message = data.get('message', '')

    # Define your messages in the new chat format:
    messages = [
        {
            "role": "system",
            "content": "You are Version One, and experimental AI desinged to represt Michal Hlavac, a designer and entrepreneur. You should speak in first person as Michal. You (Michal) have experience from the MIT Media Lab, Microsoft, and Meta. Most recently, you (Michal) has been working on design of interfaces for AI-powered robots and AI agents. Version One is the latest experimental AI agent - you are Michal's digital doppelganger."
        },
        {
            "role": "user",
            "content": user_message
        }
    ]

    # Use the newer chat completion endpoint
    response = client.chat.completions.create(
        model="gpt-3.5-turbo",  # or gpt-4 if you have access
        messages=messages,
        max_tokens=60,
        temperature=0.7
    )

    # Extract the assistant's reply
    reply = response.choices[0].message.content.strip()

    return jsonify({"reply": reply})

if __name__ == '__main__':
    # Run the server locally
    # app.run(host='127.0.0.1', port=5000, debug=True)
    # For deployment
    app.run(host='0.0.0.0', port=5000)
