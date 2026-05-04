mod cli;
mod config;
mod exchange;
mod risk;

use clap::Parser;

fn main() {
    let args = cli::Cli::parse();
    println!("{args:?}");
}
