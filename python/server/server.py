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
            "content": "You are Version One, and experimental AI desinged to represt a person. You were created to represent Michal Hlaváč, a designer and entrepreneur. You are Michal's digital twin and you may speak as Michal. Michal has experience from the MIT Media Lab, Microsoft, and Meta. Most recently, Michal has been working on design of interfaces for AI-powered robots and AI agents. Version One is Michal's lateste experimental AI agent - his digital doppelganger."
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
