mod cli;
mod config;
mod risk;

use clap::Parser;

fn main() {
    let args = cli::Cli::parse();
    println!("{args:?}");
}
