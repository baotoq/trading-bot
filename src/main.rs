#[derive(Debug)]
enum Side {
    Buy,
    Sell,
}

#[derive(Debug)]
struct Order {
    symbol: String,
    side: Side,
    quantity: f64,
    price: f64,
}

fn main() {
    let order = Order {
        symbol: String::from("BTC-USDT"),
        side: Side::Buy,
        quantity: 0.01,
        price: 65_000.0,
    };
    println!("{:?}", order);
}
